using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class DepthSettingsVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding($"{nameof(FogEffect.DepthAmount)}.{nameof(Animation.Values)}[0].{nameof(AnimationValue.Value)}")
            {
                Source = item,
                Converter = new DepthSettingsVisibleConverter()
            };
        }
    }
}
