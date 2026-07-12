namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal static class RenderingConstants
{
    public const float DefaultModelSize = 800.0f;

    public const float MinFieldOfView = 1.0f;
    public const float MaxFieldOfView = 179.0f;
    public const float DefaultFieldOfView = 45.0f;
    public const float NearPlaneRatio = 0.04f;
    public const float FarPlaneRatio = 400.0f;

    public const int MinRenderSize = 1;
    public const int MaxRenderSize = 8192;

    public const int CbSlotPerFrame = 0;
    public const int CbSlotPerObject = 1;
    public const int CbSlotPerMaterial = 2;
    public const int SlotBaseColorTexture = 0;
    public const int SlotMetallicRoughnessTexture = 1;
}
