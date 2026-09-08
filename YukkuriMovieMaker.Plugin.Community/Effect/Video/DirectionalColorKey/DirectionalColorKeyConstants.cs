using YukkuriMovieMaker.Plugin.Community.Commons.Compute;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey;

// 値は DirectionalColorKeyCS.hlsli の SMOOTH_ 定義に一致させる。
internal static class DirectionSmoothConstants
{
    public const int GroupSize = ComputeShaderDevice.PixelGroupSize;
    public const int Radius = 4;
    public const int TileSize = GroupSize + Radius * 2;
    public const int TileCount = TileSize * TileSize;
    public const int SpaceTableStride = Radius * 2 + 1;
    public const int SpaceTableCount = SpaceTableStride * SpaceTableStride;
}
