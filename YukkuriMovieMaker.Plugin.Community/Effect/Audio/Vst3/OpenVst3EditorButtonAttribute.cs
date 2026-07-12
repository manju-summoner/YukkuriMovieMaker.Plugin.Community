using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal class OpenVst3EditorButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new OpenVst3EditorButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not OpenVst3EditorButton editor)
                return;
            editor.ItemProperties = itemProperties;
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not OpenVst3EditorButton editor)
                return;
            editor.ItemProperties = null;
        }
    }
}
