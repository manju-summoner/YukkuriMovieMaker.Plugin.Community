using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// グリッドメッシュ・輪郭メッシュで共有するメッシュ幾何ヘルパー。
    /// </summary>
    internal static class ArapMeshGeometry
    {
        //スリバー三角形の過大なcot（鋭角対頂）をクランプする上限。20 ≈ cot(2.9°)。
        //鈍角対頂の負値は0にクランプする（負重みはARAPの反復を発振させるため）
        const double MaxCotan = 20.0;

        /// <summary>
        /// 三角形リストからcotan重み付きエッジ配列を構築する。
        /// 各エッジについて対頂角のcotの半分を隣接三角形分だけ加算する。
        /// </summary>
        public static (int A, int B, double Weight)[] BuildCotanEdges(Vector2[] restPositions, int[] triangleIndices)
        {
            var weights = new Dictionary<(int, int), double>();
            for (var t = 0; t < triangleIndices.Length; t += 3)
            {
                var i0 = triangleIndices[t];
                var i1 = triangleIndices[t + 1];
                var i2 = triangleIndices[t + 2];
                AddCotan(weights, restPositions, i0, i1, i2);
                AddCotan(weights, restPositions, i1, i2, i0);
                AddCotan(weights, restPositions, i2, i0, i1);
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

        static void AddCotan(Dictionary<(int, int), double> weights, Vector2[] restPositions, int a, int b, int opposite)
        {
            var pa = restPositions[a];
            var pb = restPositions[b];
            var po = restPositions[opposite];
            double uax = pa.X - po.X, uay = pa.Y - po.Y;
            double ubx = pb.X - po.X, uby = pb.Y - po.Y;
            var cross = Math.Abs(uax * uby - uay * ubx);
            if (cross < 1e-12)
                return;
            var cot = Math.Clamp((uax * ubx + uay * uby) / cross, 0.0, MaxCotan);

            var key = a < b ? (a, b) : (b, a);
            weights.TryGetValue(key, out var w);
            weights[key] = w + 0.5 * cot;
        }

        /// <summary>
        /// 三角形に対する点の重心座標を求める。僅かな範囲外は[0,1]に丸めて正規化する。
        /// </summary>
        public static (double, double, double) Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var (w0, w1, w2) = BarycentricRaw(p, a, b, c);

            //クランプ位置由来の僅かな範囲外は[0,1]に丸めて正規化する
            w1 = Math.Clamp(w1, 0, 1);
            w2 = Math.Clamp(w2, 0, 1);
            w0 = 1 - w1 - w2;
            if (w0 < 0)
            {
                var sum = w1 + w2;
                w1 /= sum;
                w2 /= sum;
                w0 = 0;
            }
            return (w0, w1, w2);
        }

        /// <summary>
        /// クランプなしの重心座標。内包判定（全成分が非負か）に使う。
        /// 退化三角形は(1,0,0)を返す。
        /// </summary>
        public static (double, double, double) BarycentricRaw(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            double v0x = b.X - a.X, v0y = b.Y - a.Y;
            double v1x = c.X - a.X, v1y = c.Y - a.Y;
            double v2x = p.X - a.X, v2y = p.Y - a.Y;
            var denom = v0x * v1y - v1x * v0y;
            if (Math.Abs(denom) < 1e-12)
                return (1, 0, 0);
            var w1 = (v2x * v1y - v1x * v2y) / denom;
            var w2 = (v0x * v2y - v2x * v0y) / denom;
            return (1 - w1 - w2, w1, w2);
        }
    }
}
