using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// アルファ輪郭に沿った不規則三角形メッシュ。
    /// 座標系は画像中央原点のローカル座標（ピンと同じ空間）。
    /// 頂点はyバケット順に番号付けされており、SolverBandwidthがBandCholeskyの帯幅を保証する。
    /// 点位置特定は三角形バケットグリッドで行う。
    /// </summary>
    internal sealed class ArapContourMesh : IArapMesh
    {
        public float Width { get; }
        public float Height { get; }
        public Vector2[] RestPositions { get; }
        public int[] TriangleIndices { get; }
        public (int A, int B, double Weight)[] Edges { get; }
        public int SolverBandwidth { get; }

        public int VertexCount => RestPositions.Length;
        public int TriangleCount => TriangleIndices.Length / 3;

        //三角形バケットグリッド（三角形のAABBが重なる全セルに登録）
        readonly float bucketSize;
        readonly float bucketMinX;
        readonly float bucketMinY;
        readonly int bucketCols;
        readonly int bucketRows;
        readonly List<int>[] buckets;

        public ArapContourMesh(float width, float height, Vector2[] restPositions, int[] triangleIndices, int solverBandwidth, float bucketSize)
        {
            Width = width;
            Height = height;
            RestPositions = restPositions;
            TriangleIndices = triangleIndices;
            SolverBandwidth = solverBandwidth;
            Edges = ArapMeshGeometry.BuildCotanEdges(restPositions, triangleIndices);

            this.bucketSize = MathF.Max(bucketSize, 1f);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in restPositions)
            {
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
            }
            bucketMinX = minX;
            bucketMinY = minY;
            bucketCols = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / this.bucketSize));
            bucketRows = Math.Max(1, (int)MathF.Ceiling((maxY - minY) / this.bucketSize));
            buckets = new List<int>[bucketCols * bucketRows];

            for (var t = 0; t < triangleIndices.Length; t += 3)
            {
                var a = restPositions[triangleIndices[t]];
                var b = restPositions[triangleIndices[t + 1]];
                var c = restPositions[triangleIndices[t + 2]];
                var (c0, r0) = LocateBucket(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)));
                var (c1, r1) = LocateBucket(MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));
                for (var row = r0; row <= r1; row++)
                {
                    for (var col = c0; col <= c1; col++)
                    {
                        var list = buckets[row * bucketCols + col] ??= [];
                        list.Add(t);
                    }
                }
            }
        }

        (int Col, int Row) LocateBucket(float x, float y)
        {
            var col = Math.Clamp((int)MathF.Floor((x - bucketMinX) / bucketSize), 0, bucketCols - 1);
            var row = Math.Clamp((int)MathF.Floor((y - bucketMinY) / bucketSize), 0, bucketRows - 1);
            return (col, row);
        }

        /// <summary>
        /// ローカル座標の点が属する三角形と重心座標を求める。
        /// メッシュ外・除去領域の点は最寄り三角形（重心距離）へアタッチする。
        /// </summary>
        public (int V0, int V1, int V2, double B0, double B1, double B2) FindContainingTriangle(Vector2 p)
        {
            var (col, row) = LocateBucket(p.X, p.Y);
            var list = buckets[row * bucketCols + col];
            if (list is not null)
            {
                foreach (var t in list)
                {
                    var a = TriangleIndices[t];
                    var b = TriangleIndices[t + 1];
                    var c = TriangleIndices[t + 2];
                    var (w0, w1, w2) = ArapMeshGeometry.BarycentricRaw(p, RestPositions[a], RestPositions[b], RestPositions[c]);
                    if (w0 >= -1e-4 && w1 >= -1e-4 && w2 >= -1e-4)
                        return Attach(a, b, c, p);
                }
            }

            //どの三角形にも含まれない点は最寄りの三角形へアタッチする（グリッドメッシュと同じ扱い）
            var bestT = 0;
            var bestDistSq = float.MaxValue;
            for (var t = 0; t < TriangleIndices.Length; t += 3)
            {
                var centroid = (
                    RestPositions[TriangleIndices[t]] +
                    RestPositions[TriangleIndices[t + 1]] +
                    RestPositions[TriangleIndices[t + 2]]) / 3f;
                var distSq = Vector2.DistanceSquared(centroid, p);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                }
            }
            return Attach(TriangleIndices[bestT], TriangleIndices[bestT + 1], TriangleIndices[bestT + 2], p);
        }

        (int, int, int, double, double, double) Attach(int a, int b, int c, Vector2 p)
        {
            var (b0, b1, b2) = ArapMeshGeometry.Barycentric(p, RestPositions[a], RestPositions[b], RestPositions[c]);
            return (a, b, c, b0, b1, b2);
        }
    }
}
