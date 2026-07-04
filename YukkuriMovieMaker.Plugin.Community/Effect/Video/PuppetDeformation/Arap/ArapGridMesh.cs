using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// ARAP変形用のグリッド三角形メッシュ。
    /// 座標系は画像中央原点のローカル座標（ピンと同じ空間）。
    /// アルファマスクで三角形を間引くと、透明領域で隔てられた部位の接続が切れる。
    /// </summary>
    internal sealed class ArapGridMesh
    {
        public float Width { get; }
        public float Height { get; }
        public int CellsX { get; }
        public int CellsY { get; }
        public float CellWidth { get; }
        public float CellHeight { get; }

        /// <summary>レスト状態の頂点位置（ローカル座標）。マスク適用後も全グリッド頂点を保持する</summary>
        public Vector2[] RestPositions { get; }

        /// <summary>三角形リスト（3頂点インデックスずつ）。マスク適用後は残存三角形のみ</summary>
        public int[] TriangleIndices { get; }

        /// <summary>重複なしのエッジと対応するcotan重み（残存三角形から構築）</summary>
        public (int A, int B, double Weight)[] Edges { get; }

        /// <summary>フルグリッドの三角形index(セル順×2)ごとの残存フラグ。未マスク時はnull</summary>
        readonly bool[]? keptFullTriangles;

        public int VertexCount => RestPositions.Length;
        public int TriangleCount => TriangleIndices.Length / 3;

        /// <summary>フルグリッドでの総三角形数（マスクの入力サイズ）</summary>
        public int FullTriangleCount => CellsX * CellsY * 2;

        ArapGridMesh(float width, float height, int cellsX, int cellsY, Vector2[] restPositions, bool[]? keepFullTriangles)
        {
            Width = width;
            Height = height;
            CellsX = cellsX;
            CellsY = cellsY;
            CellWidth = width / cellsX;
            CellHeight = height / cellsY;
            RestPositions = restPositions;
            keptFullTriangles = keepFullTriangles;

            TriangleIndices = BuildTriangles(keepFullTriangles);
            Edges = BuildCotanEdges();
        }

        /// <summary>
        /// 画像サイズと三角形数上限からメッシュを生成する。
        /// </summary>
        public static ArapGridMesh Create(float width, float height, int maxTriangles, float minSpacing)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            //三角形数 = 2 * cellsX * cellsY ≦ maxTriangles となる間隔を選ぶ
            var spacing = MathF.Max(minSpacing, MathF.Sqrt(2f * width * height / maxTriangles));
            var cellsX = Math.Max(1, (int)MathF.Floor(width / spacing));
            var cellsY = Math.Max(1, (int)MathF.Floor(height / spacing));

            //端数切り捨てでも上限を超えないよう保険
            while (2 * cellsX * cellsY > maxTriangles)
            {
                if (cellsX >= cellsY) cellsX--;
                else cellsY--;
            }

            var vertsX = cellsX + 1;
            var vertsY = cellsY + 1;
            var restPositions = new Vector2[vertsX * vertsY];
            var cellWidth = width / cellsX;
            var cellHeight = height / cellsY;
            for (var iy = 0; iy < vertsY; iy++)
            {
                var y = -height * 0.5f + cellHeight * iy;
                for (var ix = 0; ix < vertsX; ix++)
                {
                    var x = -width * 0.5f + cellWidth * ix;
                    restPositions[iy * vertsX + ix] = new Vector2(x, y);
                }
            }

            return new ArapGridMesh(width, height, cellsX, cellsY, restPositions, null);
        }

        /// <summary>
        /// フルグリッドの三角形残存フラグを適用した新しいメッシュを返す。
        /// 残存三角形が1つも無い場合はマスクを無視して自身を返す。
        /// </summary>
        public ArapGridMesh WithTriangleMask(bool[] keepFullTriangles)
        {
            if (keepFullTriangles.Length != FullTriangleCount)
                throw new ArgumentException("マスクの長さがフルグリッドの三角形数と一致していません", nameof(keepFullTriangles));

            var any = false;
            foreach (var keep in keepFullTriangles)
            {
                if (keep)
                {
                    any = true;
                    break;
                }
            }
            if (!any)
                return this;

            return new ArapGridMesh(Width, Height, CellsX, CellsY, RestPositions, keepFullTriangles);
        }

        int[] BuildTriangles(bool[]? keepFullTriangles)
        {
            var vertsX = CellsX + 1;
            var indices = new List<int>(CellsX * CellsY * 6);
            for (var iy = 0; iy < CellsY; iy++)
            {
                for (var ix = 0; ix < CellsX; ix++)
                {
                    var cellTriangle = (iy * CellsX + ix) * 2;
                    var v00 = iy * vertsX + ix;
                    var v10 = v00 + 1;
                    var v01 = v00 + vertsX;
                    var v11 = v01 + 1;

                    //セルごとに対角線の向きを交互にして変形の異方性を抑える。
                    //三角形の並び順(slot 0/1)はGetFullTriangleIndexAt/FindContainingTriangleと一致させること
                    if (((ix + iy) & 1) == 0)
                    {
                        if (keepFullTriangles is null || keepFullTriangles[cellTriangle])
                        {
                            indices.Add(v00); indices.Add(v10); indices.Add(v11);
                        }
                        if (keepFullTriangles is null || keepFullTriangles[cellTriangle + 1])
                        {
                            indices.Add(v00); indices.Add(v11); indices.Add(v01);
                        }
                    }
                    else
                    {
                        if (keepFullTriangles is null || keepFullTriangles[cellTriangle])
                        {
                            indices.Add(v00); indices.Add(v10); indices.Add(v01);
                        }
                        if (keepFullTriangles is null || keepFullTriangles[cellTriangle + 1])
                        {
                            indices.Add(v10); indices.Add(v11); indices.Add(v01);
                        }
                    }
                }
            }
            return [.. indices];
        }

        /// <summary>
        /// ローカル座標の点が属するフルグリッド三角形のindexを求める（マスク適用前の番号）。
        /// アルファマスク構築時のピクセル→三角形対応付けに使う。
        /// </summary>
        public int GetFullTriangleIndexAt(Vector2 p)
        {
            var (ix, iy, lx, ly) = LocateCell(p);
            return (iy * CellsX + ix) * 2 + GetCellTriangleSlot(ix, iy, lx, ly);
        }

        /// <summary>
        /// ローカル座標の点が属する三角形と重心座標を求める。範囲外はメッシュ内にクランプする。
        /// マスク適用後のメッシュでは、除去済み三角形上の点は最寄りの残存三角形へアタッチする。
        /// </summary>
        public (int V0, int V1, int V2, double B0, double B1, double B2) FindContainingTriangle(Vector2 p)
        {
            var (ix, iy, lx, ly) = LocateCell(p);
            var slot = GetCellTriangleSlot(ix, iy, lx, ly);
            var fullIndex = (iy * CellsX + ix) * 2 + slot;

            int a, b, c;
            if (keptFullTriangles is null || keptFullTriangles[fullIndex])
            {
                (a, b, c) = GetCellTriangleVertices(ix, iy, slot);
            }
            else
            {
                //除去済み三角形上のピンは重心が最寄りの残存三角形に付け替える
                (a, b, c) = FindNearestKeptTriangle(p);
            }

            var (b0, b1, b2) = Barycentric(p, RestPositions[a], RestPositions[b], RestPositions[c]);
            return (a, b, c, b0, b1, b2);
        }

        (int ix, int iy, float lx, float ly) LocateCell(Vector2 p)
        {
            var fx = (p.X + Width * 0.5f) / CellWidth;
            var fy = (p.Y + Height * 0.5f) / CellHeight;
            var ix = Math.Clamp((int)MathF.Floor(fx), 0, CellsX - 1);
            var iy = Math.Clamp((int)MathF.Floor(fy), 0, CellsY - 1);
            var lx = Math.Clamp(fx - ix, 0f, 1f);
            var ly = Math.Clamp(fy - iy, 0f, 1f);
            return (ix, iy, lx, ly);
        }

        static int GetCellTriangleSlot(int ix, int iy, float lx, float ly)
        {
            if (((ix + iy) & 1) == 0)
            {
                //対角線 v00-v11: 下側(ly <= lx)がslot0=(v00,v10,v11)、上側がslot1=(v00,v11,v01)
                return ly <= lx ? 0 : 1;
            }
            //対角線 v10-v01: 左下側(lx + ly <= 1)がslot0=(v00,v10,v01)、右上側がslot1=(v10,v11,v01)
            return lx + ly <= 1f ? 0 : 1;
        }

        (int A, int B, int C) GetCellTriangleVertices(int ix, int iy, int slot)
        {
            var vertsX = CellsX + 1;
            var v00 = iy * vertsX + ix;
            var v10 = v00 + 1;
            var v01 = v00 + vertsX;
            var v11 = v01 + 1;

            if (((ix + iy) & 1) == 0)
                return slot == 0 ? (v00, v10, v11) : (v00, v11, v01);
            return slot == 0 ? (v00, v10, v01) : (v10, v11, v01);
        }

        (int A, int B, int C) FindNearestKeptTriangle(Vector2 p)
        {
            var best = (A: TriangleIndices[0], B: TriangleIndices[1], C: TriangleIndices[2]);
            var bestDistSq = float.MaxValue;
            for (var t = 0; t < TriangleIndices.Length; t += 3)
            {
                var a = TriangleIndices[t];
                var b = TriangleIndices[t + 1];
                var c = TriangleIndices[t + 2];
                var centroid = (RestPositions[a] + RestPositions[b] + RestPositions[c]) / 3f;
                var distSq = Vector2.DistanceSquared(centroid, p);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = (a, b, c);
                }
            }
            return best;
        }

        (int A, int B, double Weight)[] BuildCotanEdges()
        {
            //cotan重み: 各エッジについて、対頂角のcotの半分を隣接三角形分だけ加算する
            var weights = new Dictionary<(int, int), double>();
            for (var t = 0; t < TriangleIndices.Length; t += 3)
            {
                var i0 = TriangleIndices[t];
                var i1 = TriangleIndices[t + 1];
                var i2 = TriangleIndices[t + 2];
                AddCotan(weights, i0, i1, i2);
                AddCotan(weights, i1, i2, i0);
                AddCotan(weights, i2, i0, i1);
            }

            var edges = new List<(int, int, double)>(weights.Count);
            foreach (var ((a, b), w) in weights)
            {
                //数値誤差による負値はゼロ扱い、重みゼロのエッジ（直角対頂）は除外する
                if (w > 1e-9)
                    edges.Add((a, b, w));
            }
            return [.. edges];
        }

        void AddCotan(Dictionary<(int, int), double> weights, int a, int b, int opposite)
        {
            var pa = RestPositions[a];
            var pb = RestPositions[b];
            var po = RestPositions[opposite];
            double uax = pa.X - po.X, uay = pa.Y - po.Y;
            double ubx = pb.X - po.X, uby = pb.Y - po.Y;
            var cross = Math.Abs(uax * uby - uay * ubx);
            if (cross < 1e-12)
                return;
            var cot = (uax * ubx + uay * uby) / cross;

            var key = a < b ? (a, b) : (b, a);
            weights.TryGetValue(key, out var w);
            weights[key] = w + 0.5 * cot;
        }

        static (double, double, double) Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            double v0x = b.X - a.X, v0y = b.Y - a.Y;
            double v1x = c.X - a.X, v1y = c.Y - a.Y;
            double v2x = p.X - a.X, v2y = p.Y - a.Y;
            var denom = v0x * v1y - v1x * v0y;
            if (Math.Abs(denom) < 1e-12)
                return (1, 0, 0);
            var w1 = (v2x * v1y - v1x * v2y) / denom;
            var w2 = (v0x * v2y - v2x * v0y) / denom;

            //クランプ位置由来の僅かな範囲外は[0,1]に丸めて正規化する
            w1 = Math.Clamp(w1, 0, 1);
            w2 = Math.Clamp(w2, 0, 1);
            var w0 = 1 - w1 - w2;
            if (w0 < 0)
            {
                var sum = w1 + w2;
                w1 /= sum;
                w2 /= sum;
                w0 = 0;
            }
            return (w0, w1, w2);
        }
    }
}
