using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    internal class BandsEditorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new BandsEditor();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not BandsEditor editor)
                return;
            editor.DataContext = new BandsEditorViewModel(itemProperties);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not BandsEditor editor)
                return;
            var vm = editor.DataContext as BandsEditorViewModel;
            vm?.Dispose();
            editor.DataContext = null;
        }
    }
}
