using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class Vst3EditorVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(Vst3Effect.HasEditor))
            {
                Source = item,
                Converter = new Vst3EditorVisibleConverter(),
            };
        }
    }
}
