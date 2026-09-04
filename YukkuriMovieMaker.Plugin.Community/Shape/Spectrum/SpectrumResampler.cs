namespace YukkuriMovieMaker.Plugin.Community.Shape.Spectrum
{
    internal static class SpectrumResampler
    {
        public static int Resample(float[]? source, Span<float> destination)
        {
            var length = source?.Length ?? 0;
            if (length <= 0 || destination.Length <= 0)
            {
                destination.Clear();
                return 0;
            }

            var count = Math.Min(length, destination.Length);
            for (var i = 0; i < count; i++)
            {
                var begin = (int)((long)i * length / count);
                var end = (int)((long)(i + 1) * length / count);
                if (end <= begin)
                    end = begin + 1;

                var extremum = 0f;
                var magnitude = 0f;
                for (var j = begin; j < end && j < length; j++)
                {
                    var value = source![j];
                    if (!float.IsFinite(value))
                        continue;

                    var scale = Math.Abs(value);
                    if (scale > magnitude)
                    {
                        magnitude = scale;
                        extremum = value;
                    }
                }

                destination[i] = Math.Clamp(extremum, -1f, 1f);
            }

            destination[count..].Clear();
            return count;
        }
    }
}
