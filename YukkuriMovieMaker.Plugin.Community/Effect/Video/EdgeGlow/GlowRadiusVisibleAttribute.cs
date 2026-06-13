using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class GlowRadiusVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(EdgeGlowEffect.EnableGlowSpread))
            {
                Source = item,
                Converter = new GlowRadiusVisibleConverter()
            };
        }
    }
}
