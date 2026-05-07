using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class CustomGradientStopsVisibleAttribute : Attribute, ICustomVisibilityAttribute2
{
    public Binding GetBinding(object item, object propertyOwner)
    {
        return new Binding(nameof(Effect.GradientMapEffect.GradientFilePath))
        {
            Source = item,
            Converter = new CustomGradientStopsVisibleConverter(),
        };
    }
}
