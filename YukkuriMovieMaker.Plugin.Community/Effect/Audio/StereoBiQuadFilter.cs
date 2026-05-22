using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio
{
    internal sealed class StereoBiQuadFilter
    {
        double b0, b1, b2, a1, a2;
        double z1L, z2L, z1R, z2R;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (float left, float right) Transform(float left, float right)
        {
            double outL = b0 * left + z1L;
            z1L = b1 * left - a1 * outL + z2L;
            z2L = b2 * left - a2 * outL;

            double outR = b0 * right + z1R;
            z1R = b1 * right - a1 * outR + z2R;
            z2R = b2 * right - a2 * outR;

            return ((float)outL, (float)outR);
        }

        public void SetHighShelf(int hz, float cutoffFrequency, float shelfSlope, float dbGain)
        {
            double A = Math.Pow(10.0, dbGain / 40.0);
            double w0 = 2.0 * Math.PI * cutoffFrequency / hz;
            double cosw0 = Math.Cos(w0);
            double sinw0 = Math.Sin(w0);
            double alpha = sinw0 / 2.0 * Math.Sqrt((A + 1.0 / A) * (1.0 / shelfSlope - 1.0) + 2.0);
            double sqrtA2alpha = 2.0 * Math.Sqrt(A) * alpha;

            double a0 = (A + 1.0) - (A - 1.0) * cosw0 + sqrtA2alpha;
            double inv_a0 = 1.0 / a0;

            b0 = A * ((A + 1.0) + (A - 1.0) * cosw0 + sqrtA2alpha) * inv_a0;
            b1 = -2.0 * A * ((A - 1.0) + (A + 1.0) * cosw0) * inv_a0;
            b2 = A * ((A + 1.0) + (A - 1.0) * cosw0 - sqrtA2alpha) * inv_a0;
            a1 = 2.0 * ((A - 1.0) - (A + 1.0) * cosw0) * inv_a0;
            a2 = ((A + 1.0) - (A - 1.0) * cosw0 - sqrtA2alpha) * inv_a0;
        }

        public void SetHighPass(int hz, float cutoffFrequency, float q)
        {
            double w0 = 2.0 * Math.PI * cutoffFrequency / hz;
            double cosw0 = Math.Cos(w0);
            double sinw0 = Math.Sin(w0);
            double alpha = sinw0 / (2.0 * q);

            double a0 = 1.0 + alpha;
            double inv_a0 = 1.0 / a0;

            b0 = (1.0 + cosw0) / 2.0 * inv_a0;
            b1 = -(1.0 + cosw0) * inv_a0;
            b2 = (1.0 + cosw0) / 2.0 * inv_a0;
            a1 = -2.0 * cosw0 * inv_a0;
            a2 = (1.0 - alpha) * inv_a0;
        }

        public void Reset()
        {
            z1L = z2L = z1R = z2R = 0.0;
        }
    }
}
