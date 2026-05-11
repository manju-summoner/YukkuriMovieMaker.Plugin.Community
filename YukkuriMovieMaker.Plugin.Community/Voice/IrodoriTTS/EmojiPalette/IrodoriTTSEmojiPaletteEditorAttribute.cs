using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;

internal class IrodoriTTSEmojiPaletteEditorAttribute : PropertyEditorAttribute
{
    public IrodoriTTSEmojiPaletteEditorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create()
    {
        return new IrodoriTTSEmojiPaletteView();
    }

    public override void SetBindings(FrameworkElement control, object item, object propertyOwner, PropertyInfo propertyInfo)
    {
        // IPropertyEditorControl2.SetEditorInfo で ViewModel を設定するため、ここでは何もしない
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is IrodoriTTSEmojiPaletteView view)
            view.ClearViewModel();
    }
}
