using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal class PuppetPinListEditorAttribute : PropertyEditorAttribute2
    {
        public PuppetPinListEditorAttribute()
        {
            PropertyEditorSize = PropertyEditorSize.FullWidth;
        }

        public override FrameworkElement Create()
        {
            return new PuppetPinListEditor();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is PuppetPinListEditor editor)
            {
                editor.ItemProperties = itemProperties;
                editor.DataContext = new PuppetPinListEditorViewModel(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is PuppetPinListEditor editor)
            {
                editor.ItemProperties = null;
                editor.DataContext = null;
            }
        }
    }
}
