using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class BandWidthVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(WaveClippingEffect.Mode))
            {
                Source = item,
                Converter = new BandWidthVisibleConverter()
            };
        }
    }
}
