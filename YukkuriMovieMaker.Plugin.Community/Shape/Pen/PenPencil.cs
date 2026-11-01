using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenPencil
    {
        public const int GrainSize = 256;
        public const int Levels = 16;
        public const int LevelHysteresis = 2;

        const uint GrainSeed = 0x9E3779B9;
        const float GrainLow = 0.35f;
        const float GrainHigh = 0.65f;
        const float GrainFloor = 0.25f;

        static readonly byte[] grain = CreateGrain();

        public static int GetLevel(float pressure, byte alpha)
        {
            var opacity = alpha / 255f * pressure / SerializableStylusPoint.NeutralPressure;
            if (opacity <= 0)
                return 0;

            var level = (int)MathF.Ceiling(opacity * Levels);
            return level > Levels ? Levels : level;
        }

        public static float GetOpacity(int level) => (float)level / Levels;

        public static byte[] CreatePixels(Color color)
        {
            var pixels = new byte[grain.Length * 4];
            for (var i = 0; i < grain.Length; i++)
            {
                var alpha = grain[i];
                pixels[i * 4] = (byte)(color.B * alpha / 255);
                pixels[i * 4 + 1] = (byte)(color.G * alpha / 255);
                pixels[i * 4 + 2] = (byte)(color.R * alpha / 255);
                pixels[i * 4 + 3] = alpha;
            }
            return pixels;
        }

        static byte[] CreateGrain()
        {
            var noise = new float[GrainSize * GrainSize];
            var state = GrainSeed;
            for (var i = 0; i < noise.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                noise[i] = (state >> 8) * (1f / (1 << 24));
            }

            var result = new byte[noise.Length];
            for (var y = 0; y < GrainSize; y++)
            {
                for (var x = 0; x < GrainSize; x++)
                {
                    var sum = 0f;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                            sum += noise[((y + dy + GrainSize) % GrainSize) * GrainSize + (x + dx + GrainSize) % GrainSize];
                    }
                    var value = Math.Clamp((sum / 9 - GrainLow) / (GrainHigh - GrainLow), 0f, 1f);
                    result[y * GrainSize + x] = (byte)MathF.Round(255 * (GrainFloor + (1 - GrainFloor) * value));
                }
            }
            return result;
        }
    }
}
