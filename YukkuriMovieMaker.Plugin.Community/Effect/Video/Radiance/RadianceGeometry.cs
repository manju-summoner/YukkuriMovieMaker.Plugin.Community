namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal static class RadianceGeometry
    {
        public const int LevelCount = 4;
        public const float MaxRange = 4096f;

        public static readonly float[] IntervalBounds = [0f, 1f / 85f, 5f / 85f, 21f / 85f, 1f];

        public static int Spacing(int level) => 2 << level;

        public static int TilesSide(int level) => 2 << level;

        public static int WorldPad(float range) => (int)MathF.Ceiling(MathF.Min(Math.Clamp(range, 1f, MaxRange), 512f)) + 2;

        public static int ProbeCount(int worldSize, int level) => Math.Max((worldSize + Spacing(level) - 1) / Spacing(level), 1);
    }
}
