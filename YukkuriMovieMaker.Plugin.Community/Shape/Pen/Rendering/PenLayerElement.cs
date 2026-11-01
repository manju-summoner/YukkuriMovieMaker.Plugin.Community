namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    readonly record struct PenLayerElement(IPenGeometry Geometry, int PointFrom, bool IsFill);
}
