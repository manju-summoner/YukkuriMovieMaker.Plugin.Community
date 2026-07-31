using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// 逐次点挿入によるDelaunay三角形分割（Bowyer-Watson法）。
    /// 三角形の隣接リンクを保持し、点位置特定は直前の挿入位置からの歩行探索、
    /// 空洞（外接円に点を含む三角形群）は隣接を辿るフラッドフィルで求めるため、
    /// 挿入順が空間的に連続していれば1点あたりほぼO(1)で挿入できる。
    /// 歩行が巡回した場合は全三角形の総当たりへフォールバックする。
    /// 制約エッジの強制は行わず、呼び出し側が中点分割（conforming化）で制約を満たす前提。
    /// 退化入力を避けるため、呼び出し側で全点に決定論的な微小ジッタを掛けておくこと。
    /// </summary>
    internal sealed class DelaunayTriangulator
    {
        readonly List<Vector2> points = [];

        struct Triangle
        {
            //頂点は反時計回り（Cross(A,B,C) > 0）
            public int A, B, C;
            //各エッジ（A-B, B-C, C-A）の向かいの三角形index。-1は外周
            public int NAB, NBC, NCA;
            public double CenterX, CenterY, RadiusSq;
            public bool Alive;
        }

        readonly List<Triangle> triangles = [];

        //挿入ごとの作業バッファ（再確保を避ける）
        readonly List<int> badTriangles = [];
        readonly Queue<int> floodQueue = new();
        readonly HashSet<int> floodVisited = [];
        readonly HashSet<int> badLookup = [];
        readonly List<(int Start, int End, int Outer)> boundaryEdges = [];
        readonly Dictionary<int, int> fanByStart = [];
        readonly Dictionary<int, int> fanByEnd = [];

        //歩行探索の開始三角形（直前に作った三角形。挿入順が連続していれば目標のすぐ近く）
        int walkHint;

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
            //y下向き座標で反時計回り（Crossが正）になる並び
            AddTriangle(0, 2, 1);
            walkHint = 0;
        }

        /// <summary>
        /// 点を挿入し、点のインデックスを返す。
        /// 既存点と一致する等で挿入できない場合は-1を返す（三角形分割は変化しない）。
        /// </summary>
        public int InsertPoint(Vector2 p)
        {
            badTriangles.Clear();
            badLookup.Clear();

            var start = WalkToContaining(p);
            if (start >= 0)
            {
                //隣接フラッドで外接円に点を含む三角形を集める。
                //所属三角形は数学的には必ず外接円内なので、数値誤差で弾かれても強制的に含める
                floodQueue.Clear();
                floodVisited.Clear();
                floodQueue.Enqueue(start);
                floodVisited.Add(start);
                while (floodQueue.Count > 0)
                {
                    var t = floodQueue.Dequeue();
                    var tri = triangles[t];
                    if (t != start && !IsInCircumcircle(in tri, p))
                        continue;
                    badLookup.Add(t);
                    badTriangles.Add(t);
                    if (tri.NAB >= 0 && floodVisited.Add(tri.NAB)) floodQueue.Enqueue(tri.NAB);
                    if (tri.NBC >= 0 && floodVisited.Add(tri.NBC)) floodQueue.Enqueue(tri.NBC);
                    if (tri.NCA >= 0 && floodVisited.Add(tri.NCA)) floodQueue.Enqueue(tri.NCA);
                }
            }
            else
            {
                //歩行が巡回した場合の保険: 全生存三角形の総当たり
                for (var t = 0; t < triangles.Count; t++)
                {
                    var tri = triangles[t];
                    if (!tri.Alive || !IsInCircumcircle(in tri, p))
                        continue;
                    badLookup.Add(t);
                    badTriangles.Add(t);
                }
            }
            if (badTriangles.Count == 0)
                return -1;

            //badの各エッジのうち、向かいがbadでないものが空洞の境界
            boundaryEdges.Clear();
            foreach (var t in badTriangles)
            {
                var tri = triangles[t];
                if (tri.NAB < 0 || !badLookup.Contains(tri.NAB)) boundaryEdges.Add((tri.A, tri.B, tri.NAB));
                if (tri.NBC < 0 || !badLookup.Contains(tri.NBC)) boundaryEdges.Add((tri.B, tri.C, tri.NBC));
                if (tri.NCA < 0 || !badLookup.Contains(tri.NCA)) boundaryEdges.Add((tri.C, tri.A, tri.NCA));
            }

            //境界が単一の単純閉路であること（同じ始点が2回現れない）を変更前に確認する。
            //数値誤差で空洞がくびれた場合はこの点の挿入を諦める（ジッタ済み入力ではほぼ発生しない）
            fanByStart.Clear();
            foreach (var (a, _, _) in boundaryEdges)
            {
                if (!fanByStart.TryAdd(a, -1))
                    return -1;
            }

            var newIndex = points.Count;
            points.Add(p);

            foreach (var t in badTriangles)
            {
                var tri = triangles[t];
                tri.Alive = false;
                triangles[t] = tri;
            }

            //空洞境界と新しい点でファンを張る。境界エッジは反時計回りの空洞外周なので新三角形も反時計回りになる
            fanByEnd.Clear();
            foreach (var (a, b, outer) in boundaryEdges)
            {
                var triIndex = AddTriangle(a, b, newIndex);
                var tri = triangles[triIndex];
                tri.NAB = outer;
                triangles[triIndex] = tri;
                if (outer >= 0)
                    ReplaceNeighbor(outer, a, b, triIndex);
                fanByStart[a] = triIndex;
                fanByEnd[b] = triIndex;
            }
            //ファン内部の隣接（エッジ B→新点 と 新点→A）を張る
            foreach (var (a, b, _) in boundaryEdges)
            {
                var triIndex = fanByStart[a];
                var tri = triangles[triIndex];
                tri.NBC = fanByStart[b];
                tri.NCA = fanByEnd[a];
                triangles[triIndex] = tri;
            }

            walkHint = fanByStart[boundaryEdges[0].Start];
            return newIndex;
        }

        /// <summary>
        /// walkHintから点pを含む三角形まで歩行探索する。巡回検出時は-1（呼び出し側で総当たりへ）。
        /// </summary>
        int WalkToContaining(Vector2 p)
        {
            var t = walkHint;
            if (t < 0 || t >= triangles.Count || !triangles[t].Alive)
                return -1;

            var cap = triangles.Count + 8;
            for (var step = 0; step < cap; step++)
            {
                var tri = triangles[t];
                var pa = points[tri.A];
                var pb = points[tri.B];
                var pc = points[tri.C];
                //反時計回り三角形では、辺の外側にある点はCrossが負になる。最も負の辺の向かいへ進む
                var dAB = Cross(pa, pb, p);
                var dBC = Cross(pb, pc, p);
                var dCA = Cross(pc, pa, p);
                if (dAB >= 0 && dBC >= 0 && dCA >= 0)
                    return t;

                int next;
                if (dAB <= dBC && dAB <= dCA)
                    next = tri.NAB;
                else if (dBC <= dCA)
                    next = tri.NBC;
                else
                    next = tri.NCA;
                if (next < 0)
                    return t; //スーパートライアングル外周（点は内部にある前提なので保険）
                t = next;
            }
            return -1;
        }

        static double Cross(Vector2 a, Vector2 b, Vector2 p)
            => (double)(b.X - a.X) * (p.Y - a.Y) - (double)(b.Y - a.Y) * (p.X - a.X);

        static bool IsInCircumcircle(in Triangle tri, Vector2 p)
        {
            var dx = p.X - tri.CenterX;
            var dy = p.Y - tri.CenterY;
            var distSq = dx * dx + dy * dy;
            //数値誤差で外接円上の点を含めないよう保守側（含めない側）に倒す
            return distSq < tri.RadiusSq - Math.Max(1e-9, tri.RadiusSq * 1e-10);
        }

        /// <summary>三角形tのエッジ{x,y}の向かいをnewNeighborに張り替える</summary>
        void ReplaceNeighbor(int t, int x, int y, int newNeighbor)
        {
            var tri = triangles[t];
            if ((tri.A == x && tri.B == y) || (tri.A == y && tri.B == x))
                tri.NAB = newNeighbor;
            else if ((tri.B == x && tri.C == y) || (tri.B == y && tri.C == x))
                tri.NBC = newNeighbor;
            else if ((tri.C == x && tri.A == y) || (tri.C == y && tri.A == x))
                tri.NCA = newNeighbor;
            triangles[t] = tri;
        }

        int AddTriangle(int a, int b, int c)
        {
            var pa = points[a];
            var pb = points[b];
            var pc = points[c];
            double ax = pa.X, ay = pa.Y;
            double bx = pb.X, by = pb.Y;
            double cx = pc.X, cy = pc.Y;
            var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

            var tri = new Triangle { A = a, B = b, C = c, NAB = -1, NBC = -1, NCA = -1, Alive = true };
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
            return triangles.Count - 1;
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
