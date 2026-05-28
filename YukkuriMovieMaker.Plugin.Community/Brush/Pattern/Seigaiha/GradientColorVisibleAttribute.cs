namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class GradientColorVisibleAttribute : GradientVisibilityAttributeBase
    {
        protected override bool Invert => false;
    }
}
