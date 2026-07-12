using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class Vst3FileSelectorAttribute : PropertyEditorAttribute2
    {
        const string FilterPattern = "*.vst3";

        public Vst3FileSelectorAttribute()
        {
            PropertyEditorSize = PropertyEditorSize.FullWidth;
        }

        public override FrameworkElement Create() => new FileSelector();

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            var editor = (FileSelector)control;
            editor.FileType = FileType.None;
            editor.ShowThumbnail = false;
            editor.Filter = FilterPattern;
            editor.FilterName = Texts.Vst3FilterName;

            var currentItemProperty = itemProperties[0];
            var file = (string?)currentItemProperty.PropertyInfo.GetValue(currentItemProperty.PropertyOwner);
            editor.DirectoryPath = string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);

            var targetProperties = GetTargetProperties(itemProperties).ToArray();
            editor.SetBinding(FileSelector.ValueProperty, ItemPropertiesBinding.Create2(targetProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            BindingOperations.ClearBinding(control, FileSelector.ValueProperty);
        }

        private static IEnumerable<ItemProperty> GetTargetProperties(ItemProperty[] itemProperties)
        {
            foreach (var itemProperty in itemProperties)
            {
                if (itemProperty.PropertyInfo.GetCustomAttribute<Vst3FileSelectorAttribute>() is null)
                    continue;
                yield return itemProperty;
            }
        }
    }
}
