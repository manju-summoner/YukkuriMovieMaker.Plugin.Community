using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// ARAP変形用のグリッド三角形メッシュ。
    /// 座標系は画像中央原点のローカル座標（ピンと同じ空間）。
    /// </summary>
    internal sealed class ArapGridMesh
    {
        public float Width { get; }
        public float Height { get; }
        public int CellsX { get; }
        public int CellsY { get; }
        public float CellWidth { get; }
        public float CellHeight { get; }

        /// <summary>レスト状態の頂点位置（ローカル座標）</summary>
        public Vector2[] RestPositions { get; }

        /// <summary>三角形リスト（3頂点インデックスずつ）</summary>
        public int[] TriangleIndices { get; }

        /// <summary>重複なしのエッジと対応するcotan重み</summary>
        public (int A, int B, double Weight)[] Edges { get; }

        public int VertexCount => RestPositions.Length;
        public int TriangleCount => TriangleIndices.Length / 3;

        ArapGridMesh(float width, float height, int cellsX, int cellsY)
        {
            Width = width;
            Height = height;
            CellsX = cellsX;
            CellsY = cellsY;
            CellWidth = width / cellsX;
            CellHeight = height / cellsY;

            var vertsX = cellsX + 1;
            var vertsY = cellsY + 1;
            RestPositions = new Vector2[vertsX * vertsY];
            for (var iy = 0; iy < vertsY; iy++)
            {
                var y = -height * 0.5f + CellHeight * iy;
                for (var ix = 0; ix < vertsX; ix++)
                {
                    var x = -width * 0.5f + CellWidth * ix;
                    RestPositions[iy * vertsX + ix] = new Vector2(x, y);
                }
            }

            TriangleIndices = new int[cellsX * cellsY * 6];
            var t = 0;
            for (var iy = 0; iy < cellsY; iy++)
            {
                for (var ix = 0; ix < cellsX; ix++)
                {
                    var v00 = iy * vertsX + ix;
                    var v10 = v00 + 1;
                    var v01 = v00 + vertsX;
                    var v11 = v01 + 1;

                    //セルごとに対角線の向きを交互にして変形の異方性を抑える
                    if (((ix + iy) & 1) == 0)
                    {
                        TriangleIndices[t++] = v00; TriangleIndices[t++] = v10; TriangleIndices[t++] = v11;
                        TriangleIndices[t++] = v00; TriangleIndices[t++] = v11; TriangleIndices[t++] = v01;
                    }
                    else
                    {
                        TriangleIndices[t++] = v00; TriangleIndices[t++] = v10; TriangleIndices[t++] = v01;
                        TriangleIndices[t++] = v10; TriangleIndices[t++] = v11; TriangleIndices[t++] = v01;
                    }
                }
            }

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

            return new ArapGridMesh(width, height, cellsX, cellsY);
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

        /// <summary>
        /// ローカル座標の点が属する三角形と重心座標を求める。範囲外はメッシュ内にクランプする。
        /// </summary>
        public (int V0, int V1, int V2, double B0, double B1, double B2) FindContainingTriangle(Vector2 p)
        {
            var vertsX = CellsX + 1;

            var fx = (p.X + Width * 0.5f) / CellWidth;
            var fy = (p.Y + Height * 0.5f) / CellHeight;
            var ix = Math.Clamp((int)MathF.Floor(fx), 0, CellsX - 1);
            var iy = Math.Clamp((int)MathF.Floor(fy), 0, CellsY - 1);

            //セル内ローカル座標(0..1)
            var lx = Math.Clamp(fx - ix, 0f, 1f);
            var ly = Math.Clamp(fy - iy, 0f, 1f);

            var v00 = iy * vertsX + ix;
            var v10 = v00 + 1;
            var v01 = v00 + vertsX;
            var v11 = v01 + 1;

            int a, b, c;
            if (((ix + iy) & 1) == 0)
            {
                //対角線 v00-v11: 下側(ly <= lx)は(v00,v10,v11)、上側は(v00,v11,v01)
                if (ly <= lx) { a = v00; b = v10; c = v11; }
                else { a = v00; b = v11; c = v01; }
            }
            else
            {
                //対角線 v10-v01: 左下側(lx + ly <= 1)は(v00,v10,v01)、右上側は(v10,v11,v01)
                if (lx + ly <= 1f) { a = v00; b = v10; c = v01; }
                else { a = v10; b = v11; c = v01; }
            }

            var (b0, b1, b2) = Barycentric(p, RestPositions[a], RestPositions[b], RestPositions[c]);
            return (a, b, c, b0, b1, b2);
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
