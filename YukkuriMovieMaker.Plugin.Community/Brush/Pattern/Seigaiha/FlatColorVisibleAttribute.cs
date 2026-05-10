namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class FlatColorVisibleAttribute : GradientVisibilityAttributeBase
    {
        protected override bool Invert => true;
    }
}
