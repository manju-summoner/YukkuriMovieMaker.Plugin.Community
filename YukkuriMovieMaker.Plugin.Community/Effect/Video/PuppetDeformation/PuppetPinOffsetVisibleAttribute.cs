using System;
using System.Windows.Data;
using System.Windows.Controls;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class PuppetPinOffsetVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(PuppetPin.IsOffsetSelected))
            {
                Source = propertyOwner,
                Converter = new BooleanToVisibilityConverter(),
            };
        }
    }
}
