using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Converters
{
    public sealed class GrdEntryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? SelectedTemplate { get; set; }
        public DataTemplate? DropdownTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        {
            if (item is not GrdGradientEntry) return base.SelectTemplate(item, container);
            return IsInDropdown(container) ? DropdownTemplate : SelectedTemplate;
        }

        private static bool IsInDropdown(DependencyObject container)
        {
            var current = container;
            while (current is not null)
            {
                if (current is ComboBoxItem) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
