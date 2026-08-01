using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Dithering
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class DuotoneColorVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(DitheringEffect.Mode))
            {
                Source = item,
                Converter = new DuotoneColorVisibleConverter()
            };
        }
    }
}
