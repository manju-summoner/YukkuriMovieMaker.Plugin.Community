using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

internal class OpenNodeEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create()
    {
        return new OpenNodeEditorButton();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not OpenNodeEditorButton editor)
            return;

        editor.ItemProperties = itemProperties;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not OpenNodeEditorButton editor)
            return;
        editor.ItemProperties = null;
    }
}