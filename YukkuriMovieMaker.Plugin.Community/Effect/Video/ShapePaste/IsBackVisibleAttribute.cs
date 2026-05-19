namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class IsBackVisibleAttribute : PropertyVisibilityAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.DisplayMode);

        protected override bool IsVisible(object? value) => value is ShapeDisplayMode.Overlay;
    }
}
