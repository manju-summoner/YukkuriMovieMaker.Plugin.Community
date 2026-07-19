using System.Numerics;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    /// <summary>
    /// 解析勾配付き3次元gradient noiseから、発散のないcurlベクトルを生成する純関数。
    /// 格子点ごとにhashを1回だけ計算し、異なるbit群からベクトルポテンシャル3成分の勾配を選ぶ。
    /// </summary>
    internal static class ParticleOutputCurlNoise
    {
        internal readonly record struct NoiseSample(double Value, Vector3 Gradient);
        internal readonly record struct PotentialSample(NoiseSample X, NoiseSample Y, NoiseSample Z);

        //立方体の辺方向を正規化した12方向。固定テーブルなのでスレッド間で共有しても安全。
        static readonly Vector3[] Gradients =
        [
            Vector3.Normalize(new(1, 1, 0)), Vector3.Normalize(new(-1, 1, 0)),
            Vector3.Normalize(new(1, -1, 0)), Vector3.Normalize(new(-1, -1, 0)),
            Vector3.Normalize(new(1, 0, 1)), Vector3.Normalize(new(-1, 0, 1)),
            Vector3.Normalize(new(1, 0, -1)), Vector3.Normalize(new(-1, 0, -1)),
            Vector3.Normalize(new(0, 1, 1)), Vector3.Normalize(new(0, -1, 1)),
            Vector3.Normalize(new(0, 1, -1)), Vector3.Normalize(new(0, -1, -1)),
            Vector3.Normalize(new(1, 1, 0)), Vector3.Normalize(new(-1, 1, 0)),
            Vector3.Normalize(new(0, 1, 1)), Vector3.Normalize(new(0, -1, 1)),
        ];

        //この固定係数だけでRMSを実用域へ揃える。サンプル単位のnormalize/clampはcurl場を壊すため行わない。
        const float CurlRmsNormalization = 0.75f;

        internal static Vector3 EvaluateCurl(Vector3 position)
        {
            var potential = EvaluatePotential(position);
            return new Vector3(
                potential.Z.Gradient.Y - potential.Y.Gradient.Z,
                potential.X.Gradient.Z - potential.Z.Gradient.X,
                potential.Y.Gradient.X - potential.X.Gradient.Y) * CurlRmsNormalization;
        }

        internal static PotentialSample EvaluatePotential(Vector3 position)
            => EvaluatePotential(position.X, position.Y, position.Z);

        static PotentialSample EvaluatePotential(float x, float y, float z)
        {
            //負座標を0方向へ切り捨てない。入力は頂点と同じfloat座標なので、補間もfloatで揃える。
            var ix = (long)MathF.Floor(x);
            var iy = (long)MathF.Floor(y);
            var iz = (long)MathF.Floor(z);
            var fx = x - (float)ix;
            var fy = y - (float)iy;
            var fz = z - (float)iz;

            var u = Fade(fx);
            var v = Fade(fy);
            var w = Fade(fz);
            var du = FadeDerivative(fx);
            var dv = FadeDerivative(fy);
            var dw = FadeDerivative(fz);

            float value0 = 0, value1 = 0, value2 = 0;
            float gx0 = 0, gx1 = 0, gx2 = 0;
            float gy0 = 0, gy1 = 0, gy2 = 0;
            float gz0 = 0, gz1 = 0, gz2 = 0;

            for (var cz = 0; cz <= 1; cz++)
            for (var cy = 0; cy <= 1; cy++)
            for (var cx = 0; cx <= 1; cx++)
            {
                var wx = cx == 0 ? 1 - u : u;
                var wy = cy == 0 ? 1 - v : v;
                var wz = cz == 0 ? 1 - w : w;
                var dwx = cx == 0 ? -du : du;
                var dwy = cy == 0 ? -dv : dv;
                var dwz = cz == 0 ? -dw : dw;
                var weight = wx * wy * wz;
                var weightDx = dwx * wy * wz;
                var weightDy = wx * dwy * wz;
                var weightDz = wx * wy * dwz;
                var dx = fx - cx;
                var dy = fy - cy;
                var dz = fz - cz;

                var hash = Hash(ix + cx, iy + cy, iz + cz);
                Accumulate(Gradients[hash & 15], dx, dy, dz, weight, weightDx, weightDy, weightDz,
                    ref value0, ref gx0, ref gy0, ref gz0);
                Accumulate(Gradients[(hash >> 8) & 15], dx, dy, dz, weight, weightDx, weightDy, weightDz,
                    ref value1, ref gx1, ref gy1, ref gz1);
                Accumulate(Gradients[(hash >> 16) & 15], dx, dy, dz, weight, weightDx, weightDy, weightDz,
                    ref value2, ref gx2, ref gy2, ref gz2);
            }

            return new PotentialSample(
                new NoiseSample(value0, new Vector3(gx0, gy0, gz0)),
                new NoiseSample(value1, new Vector3(gx1, gy1, gz1)),
                new NoiseSample(value2, new Vector3(gx2, gy2, gz2)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Accumulate(
            Vector3 gradient, float dx, float dy, float dz,
            float weight, float weightDx, float weightDy, float weightDz,
            ref float value, ref float gx, ref float gy, ref float gz)
        {
            var dot = gradient.X * dx + gradient.Y * dy + gradient.Z * dz;
            value += weight * dot;
            gx += weightDx * dot + weight * gradient.X;
            gy += weightDy * dot + weight * gradient.Y;
            gz += weightDz * dot + weight * gradient.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float FadeDerivative(float t) => 30 * t * t * (t * (t - 2) + 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Hash(long x, long y, long z)
        {
            //overflowを利用するavalanche mix。checkedビルドでも意図どおり折り返す。
            unchecked
            {
                var h = (ulong)x * 0x9E3779B185EBCA87UL;
                h ^= (ulong)y * 0xC2B2AE3D27D4EB4FUL;
                h ^= (ulong)z * 0x165667B19E3779F9UL;
                h ^= h >> 30;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
