using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.DynamicEffect
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class RmsWindowVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(ConditionalDynamicsEffect.DetectionMode))
            {
                Source = item,
                Converter = new RmsWindowVisibleConverter(),
            };
        }
    }
}
