using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Skew
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class SkewCenterPointCustomVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(AudioVolumeSkewEffect.CenterPoint))
            {
                Source = item,
                Converter = new SkewCenterPointCustomVisibleConverter()
            };
        }
    }
}
