using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    readonly record struct PenLayerPlan(
        int Index,
        int PointFrom,
        int PointLength,
        double Opacity,
        Blend BlendMode,
        bool IsClipping,
        Guid Id,
        Guid ParentId,
        bool IsFolder,
        bool HasEffects);
}
