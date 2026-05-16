namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class DrawPositionVisibleAttribute : PropertyVisibilityAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.IsFullyPinned);

        protected override bool IsVisible(object? value) => value is not true;
    }
}
