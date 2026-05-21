using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class FixedColorVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(EdgeGlowEffect.ColorSource))
            {
                Source = item,
                Converter = new FixedColorVisibleConverter()
            };
        }
    }
}
