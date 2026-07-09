namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal static class RadianceGeometry
    {
        public const int LevelCount = 4;
        public const float MaxRange = 4096f;

        public const float MaxWorldPad = 1024f;

        public static readonly float[] IntervalBounds = [0f, 1f / 85f, 5f / 85f, 21f / 85f, 1f];

        public static int Spacing(int level) => 2 << level;

        public static int TilesSide(int level) => 2 << level;

        // 余白は到達距離に追従させるが、大きな入力で確保領域が過大になり破綻するのを防ぐため上限で頭打ちにする
        public static int WorldPad(float range) => (int)MathF.Ceiling(MathF.Min(Math.Clamp(range, 1f, MaxRange), MaxWorldPad)) + 2;

        public static int ProbeCount(int worldSize, int level) => Math.Max((worldSize + Spacing(level) - 1) / Spacing(level), 1);

        public const int OccLevelCount = 9;

        public static int OccBlockSize(int worldSize, int level) => Math.Max((worldSize + (1 << level) - 1) >> level, 1);

        public static int OccAtlasWidth(int worldW) => OccBlockSize(worldW, 1);

        public static int OccAtlasHeight(int worldH)
        {
            var total = 0;
            for (var level = 1; level <= OccLevelCount; level++)
                total += OccBlockSize(worldH, level);
            return total;
        }
    }
}
