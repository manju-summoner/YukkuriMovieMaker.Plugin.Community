using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PerspectiveShadow
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class WallDistanceVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(PerspectiveShadowEffect.WallEnabled))
            {
                Source = item,
                Converter = new WallDistanceVisibleConverter()
            };
        }
    }
}
