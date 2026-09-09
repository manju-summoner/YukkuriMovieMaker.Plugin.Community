namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey;

// 値は DirectionalColorKeyCS.hlsli の SMOOTH_RADIUS に一致させる。
// タイル寸法などの派生値は hlsli 側で SMOOTH_RADIUS と GROUP_X から導く。
internal static class DirectionSmoothConstants
{
    public const int Radius = 4;
}
