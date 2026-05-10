using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    internal abstract class GradientVisibilityAttributeBase : Attribute, ICustomVisibilityAttribute2
    {
        protected abstract bool Invert { get; }

        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(SeigaihaBrushParameter.GradientEnabled))
            {
                Source = item,
                Converter = new BoolToVisibilityConverter { Invert = Invert }
            };
        }
    }
}
