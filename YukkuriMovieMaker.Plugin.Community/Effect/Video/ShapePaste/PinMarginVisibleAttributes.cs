namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    internal abstract class PinMarginVisibleAttributeBase : PropertyVisibilityAttributeBase
    {
        protected override bool IsVisible(object? value) => value is true;
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class LeftMarginVisibleAttribute : PinMarginVisibleAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.PinLeft);
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class RightMarginVisibleAttribute : PinMarginVisibleAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.PinRight);
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class TopMarginVisibleAttribute : PinMarginVisibleAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.PinTop);
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class BottomMarginVisibleAttribute : PinMarginVisibleAttributeBase
    {
        protected override string SourcePropertyName => nameof(ShapePasteEffect.PinBottom);
    }
}
