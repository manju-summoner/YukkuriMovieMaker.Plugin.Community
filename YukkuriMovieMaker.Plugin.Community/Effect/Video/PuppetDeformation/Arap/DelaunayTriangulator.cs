using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// 逐次点挿入によるDelaunay三角形分割（Bowyer-Watson法）。
    /// 制約エッジの強制は行わず、呼び出し側が中点分割（conforming化）で制約を満たす前提。
    /// 外接円判定は三角形ごとに外心と半径をキャッシュした総当たりで行う
    /// （メッシュ再構築時に1回だけ実行されるため、実装の単純さと頑健さを優先する）。
    /// 退化入力を避けるため、呼び出し側で全点に決定論的な微小ジッタを掛けておくこと。
    /// </summary>
    internal sealed class DelaunayTriangulator
    {
        readonly List<Vector2> points = [];

        struct Triangle
        {
            public int A, B, C;
            public double CenterX, CenterY, RadiusSq;
            public bool Alive;
        }

        readonly List<Triangle> triangles = [];
        //挿入ごとの作業バッファ（再確保を避ける）
        readonly List<int> badTriangles = [];
        readonly Dictionary<long, int> directedEdges = [];

        /// <summary>スーパートライアングルの頂点数（先頭3点）。この頂点を含む三角形は出力から除外する</summary>
        public const int SuperVertexCount = 3;

        public int PointCount => points.Count;

        public Vector2 GetPoint(int index) => points[index];

        /// <summary>
        /// 指定範囲の点をすべて内包するスーパートライアングルで初期化する。
        /// </summary>
        public DelaunayTriangulator(Vector2 min, Vector2 max)
        {
            var cx = (min.X + max.X) * 0.5;
            var cy = (min.Y + max.Y) * 0.5;
            var m = 8.0 * Math.Max(1.0, Math.Max(max.X - min.X, max.Y - min.Y));
            points.Add(new Vector2((float)cx, (float)(cy - 2 * m)));
            points.Add(new Vector2((float)(cx - 2 * m), (float)(cy + m)));
            points.Add(new Vector2((float)(cx + 2 * m), (float)(cy + m)));
            AddTriangle(0, 1, 2);
        }

        /// <summary>
        /// 点を挿入し、点のインデックスを返す。
        /// 既存点と一致する等で挿入できない場合は-1を返す（三角形分割は変化しない）。
        /// </summary>
        public int InsertPoint(Vector2 p)
        {
            //外接円に点を含む三角形（bad）を集める
            badTriangles.Clear();
            for (var t = 0; t < triangles.Count; t++)
            {
                var tri = triangles[t];
                if (!tri.Alive)
                    continue;
                var dx = p.X - tri.CenterX;
                var dy = p.Y - tri.CenterY;
                var distSq = dx * dx + dy * dy;
                //数値誤差で外接円上の点を含めないよう保守側（含めない側）に倒す
                if (distSq < tri.RadiusSq - Math.Max(1e-9, tri.RadiusSq * 1e-10))
                    badTriangles.Add(t);
            }
            if (badTriangles.Count == 0)
                return -1;

            //badの有向エッジのうち、逆向きがbad内に存在しないものが空洞の境界
            directedEdges.Clear();
            foreach (var t in badTriangles)
            {
                var tri = triangles[t];
                AddDirectedEdge(tri.A, tri.B);
                AddDirectedEdge(tri.B, tri.C);
                AddDirectedEdge(tri.C, tri.A);
            }

            var newIndex = points.Count;
            points.Add(p);

            foreach (var t in badTriangles)
            {
                var tri = triangles[t];
                tri.Alive = false;
                triangles[t] = tri;
            }
            foreach (var (key, count) in directedEdges)
            {
                if (count == 0)
                    continue;
                var a = (int)(key >> 32);
                var b = (int)(key & 0xFFFFFFFF);
                AddTriangle(a, b, newIndex);
            }
            return newIndex;
        }

        void AddDirectedEdge(int a, int b)
        {
            //逆向きエッジが既にあれば互いに打ち消す（両方とも内部エッジ）
            var reverseKey = ((long)b << 32) | (uint)a;
            if (directedEdges.TryGetValue(reverseKey, out var rc) && rc > 0)
            {
                directedEdges[reverseKey] = rc - 1;
                return;
            }
            var key = ((long)a << 32) | (uint)b;
            directedEdges.TryGetValue(key, out var c);
            directedEdges[key] = c + 1;
        }

        void AddTriangle(int a, int b, int c)
        {
            var pa = points[a];
            var pb = points[b];
            var pc = points[c];
            double ax = pa.X, ay = pa.Y;
            double bx = pb.X, by = pb.Y;
            double cx = pc.X, cy = pc.Y;
            var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

            var tri = new Triangle { A = a, B = b, C = c, Alive = true };
            if (Math.Abs(d) < 1e-12)
            {
                //退化三角形（共線）は外接円なし扱いにして以後の挿入で壊れないようにする。
                //ジッタ済み入力ではほぼ発生しない
                tri.CenterX = ax;
                tri.CenterY = ay;
                tri.RadiusSq = 0;
            }
            else
            {
                var aSq = ax * ax + ay * ay;
                var bSq = bx * bx + by * by;
                var cSq = cx * cx + cy * cy;
                var ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / d;
                var uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / d;
                tri.CenterX = ux;
                tri.CenterY = uy;
                var rx = ax - ux;
                var ry = ay - uy;
                tri.RadiusSq = rx * rx + ry * ry;
            }
            triangles.Add(tri);
        }

        /// <summary>スーパートライアングル頂点を含まない生存三角形を列挙する</summary>
        public List<(int A, int B, int C)> GetTriangles()
        {
            var result = new List<(int, int, int)>();
            foreach (var tri in triangles)
            {
                if (!tri.Alive)
                    continue;
                if (tri.A < SuperVertexCount || tri.B < SuperVertexCount || tri.C < SuperVertexCount)
                    continue;
                result.Add((tri.A, tri.B, tri.C));
            }
            return result;
        }

        /// <summary>
        /// 生存三角形（スーパートライアングル頂点を含むものも含む）の無向エッジ集合を返す。
        /// conforming化での制約エッジ存在確認に使う。
        /// </summary>
        public HashSet<long> BuildUndirectedEdgeSet()
        {
            var set = new HashSet<long>();
            foreach (var tri in triangles)
            {
                if (!tri.Alive)
                    continue;
                set.Add(UndirectedEdgeKey(tri.A, tri.B));
                set.Add(UndirectedEdgeKey(tri.B, tri.C));
                set.Add(UndirectedEdgeKey(tri.C, tri.A));
            }
            return set;
        }

        public static long UndirectedEdgeKey(int a, int b)
            => a < b ? (((long)a << 32) | (uint)b) : (((long)b << 32) | (uint)a);
    }
}
