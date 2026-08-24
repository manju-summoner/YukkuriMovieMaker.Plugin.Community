using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointListEditorAttribute : PropertyEditorAttribute2
    {
        public VectorFieldPointListEditorAttribute()
        {
            PropertyEditorSize = PropertyEditorSize.FullWidth;
        }

        public override FrameworkElement Create() => new VectorFieldPointListEditor();

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not VectorFieldPointListEditor editor)
                return;
            editor.ItemProperties = itemProperties;
            editor.DataContext = new VectorFieldPointListEditorViewModel(itemProperties);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not VectorFieldPointListEditor editor)
                return;
            editor.ItemProperties = null;
            editor.DataContext = null;
        }
    }
}
