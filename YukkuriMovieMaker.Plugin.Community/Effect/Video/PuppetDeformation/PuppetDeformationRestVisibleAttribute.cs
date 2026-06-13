using System;
using System.Windows.Data;
using System.Windows.Controls;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class PuppetDeformationRestVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(PuppetDeformation.IsRestSelected))
            {
                Source = propertyOwner,
                Converter = new BooleanToVisibilityConverter(),
            };
        }
    }
}
