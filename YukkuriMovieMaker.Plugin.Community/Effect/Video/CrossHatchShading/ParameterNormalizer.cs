namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    internal static class ParameterNormalizer
    {
        public static float Percent(double value, float minimum, float maximum, float fallback) => Finite(value / 100d, minimum, maximum, fallback);

        public static float Finite(double value, float minimum, float maximum, float fallback)
        {
            if (!double.IsFinite(value))
                return fallback;
            return (float)Math.Clamp(value, minimum, maximum);
        }
    }
}
