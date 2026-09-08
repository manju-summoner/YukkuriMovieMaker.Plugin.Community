namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey;

internal static class DirectionSmoothConstants
{
    // シェーダーの [numthreads(8, 8, 1)] に対応する固定値。
    public const int GroupSize = 8;
    public const int Radius = 4;
    public const int TileSize = GroupSize + Radius * 2;
    public const int TileCount = TileSize * TileSize;
    public const int SpaceTableStride = Radius * 2 + 1;
    public const int SpaceTableCount = SpaceTableStride * SpaceTableStride;
}
