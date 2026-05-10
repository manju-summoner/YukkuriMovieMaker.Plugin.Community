using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class RandomModeVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(WaveClippingEffect.UseRandom))
            {
                Source = item,
                Converter = new RandomModeVisibleConverter()
            };
        }
    }
}
