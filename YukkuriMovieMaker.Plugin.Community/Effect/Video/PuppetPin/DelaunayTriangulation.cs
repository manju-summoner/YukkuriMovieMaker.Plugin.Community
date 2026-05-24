using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    internal static class DelaunayTriangulation
    {
        public struct Edge : IEquatable<Edge>
        {
            public int A, B;
            public Edge(int a, int b) { A = Math.Min(a, b); B = Math.Max(a, b); }
            public bool Equals(Edge other) => A == other.A && B == other.B;
            public override bool Equals(object? obj) => obj is Edge e && Equals(e);
            public override int GetHashCode() => HashCode.Combine(A, B);
        }

        public struct TriangleData
        {
            public int A, B, C;
            public Vector2 CircumCenter;
            public float CircumRadiusSq;

            public TriangleData(int a, int b, int c, List<Vector2> pts)
            {
                A = a; B = b; C = c;
                Vector2 p1 = pts[a], p2 = pts[b], p3 = pts[c];
                float d = 2 * (p1.X * (p2.Y - p3.Y) + p2.X * (p3.Y - p1.Y) + p3.X * (p1.Y - p2.Y));
                if (Math.Abs(d) < 1e-6f)
                {
                    CircumCenter = p1;
                    CircumRadiusSq = float.MaxValue;
                }
                else
                {
                    float ux = ((p1.X * p1.X + p1.Y * p1.Y) * (p2.Y - p3.Y) + (p2.X * p2.X + p2.Y * p2.Y) * (p3.Y - p1.Y) + (p3.X * p3.X + p3.Y * p3.Y) * (p1.Y - p2.Y)) / d;
                    float uy = ((p1.X * p1.X + p1.Y * p1.Y) * (p3.X - p2.X) + (p2.X * p2.X + p2.Y * p2.Y) * (p1.X - p3.X) + (p3.X * p3.X + p3.Y * p3.Y) * (p2.X - p1.X)) / d;
                    CircumCenter = new Vector2(ux, uy);
                    CircumRadiusSq = Vector2.DistanceSquared(CircumCenter, p1);
                }
            }
        }

        public static (List<TriangleData> triangles, List<Edge> edges) Compute(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                var ed = new List<Edge>();
                if (points.Count == 2) ed.Add(new Edge(0, 1));
                return (new List<TriangleData>(), ed);
            }

            var pts = new List<Vector2>(points);
            float minX = pts[0].X, minY = pts[0].Y, maxX = minX, maxY = minY;
            for (int i = 1; i < pts.Count; i++)
            {
                if (pts[i].X < minX) minX = pts[i].X;
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].X > maxX) maxX = pts[i].X;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }
            float dx = maxX - minX;
            float dy = maxY - minY;
            float deltaMax = Math.Max(dx, dy);
            float midx = (minX + maxX) / 2f;
            float midy = (minY + maxY) / 2f;

            pts.Add(new Vector2(midx - 20 * deltaMax, midy - deltaMax));
            pts.Add(new Vector2(midx, midy + 20 * deltaMax));
            pts.Add(new Vector2(midx + 20 * deltaMax, midy - deltaMax));

            int n = pts.Count;
            var triangles = new List<TriangleData>();
            triangles.Add(new TriangleData(n - 3, n - 2, n - 1, pts));

            var edgeSeen = new HashSet<Edge>();
            var uniqueEdges = new List<Edge>();

            for (int i = 0; i < points.Count; i++)
            {
                edgeSeen.Clear();
                uniqueEdges.Clear();

                for (int j = triangles.Count - 1; j >= 0; j--)
                {
                    var t = triangles[j];
                    if (Vector2.DistanceSquared(t.CircumCenter, pts[i]) > t.CircumRadiusSq)
                        continue;

                    triangles.RemoveAt(j);

                    TryAddUniqueEdge(new Edge(t.A, t.B), edgeSeen, uniqueEdges);
                    TryAddUniqueEdge(new Edge(t.B, t.C), edgeSeen, uniqueEdges);
                    TryAddUniqueEdge(new Edge(t.C, t.A), edgeSeen, uniqueEdges);
                }

                foreach (var edge in uniqueEdges)
                    triangles.Add(new TriangleData(edge.A, edge.B, i, pts));
            }

            var resultTriangles = new List<TriangleData>();
            var resultEdges = new HashSet<Edge>();
            foreach (var t in triangles)
            {
                if (t.A < points.Count && t.B < points.Count && t.C < points.Count)
                {
                    resultTriangles.Add(t);
                    resultEdges.Add(new Edge(t.A, t.B));
                    resultEdges.Add(new Edge(t.B, t.C));
                    resultEdges.Add(new Edge(t.C, t.A));
                }
            }

            return (resultTriangles, resultEdges.ToList());
        }

        private static void TryAddUniqueEdge(Edge edge, HashSet<Edge> seen, List<Edge> unique)
        {
            if (!seen.Add(edge))
                unique.RemoveAll(e => e.Equals(edge));
            else
                unique.Add(edge);
        }
    }
}
