using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class BlurStrengthVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(HeatHazeEffect.EnableBlur))
            {
                Source = item,
                Converter = new BlurStrengthVisibleConverter()
            };
        }
    }
}
