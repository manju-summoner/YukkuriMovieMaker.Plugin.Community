using System;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;

public partial class IrodoriTTSEmojiPaletteView : UserControl, IPropertyEditorControl2
{
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public IrodoriTTSEmojiPaletteView()
    {
        InitializeComponent();
    }

    public void SetEditorInfo(IEditorInfo? info)
    {
        var vm = new IrodoriTTSEmojiPaletteViewModel(info?.VoiceItemEdit, info);
        vm.GetCaretIndex = () => HatsuonTextBox.CaretIndex;
        vm.SetCaretIndex = index =>
        {
            HatsuonTextBox.CaretIndex = index;
            HatsuonTextBox.Focus();
        };
        DataContext = vm;
    }

    public void ClearViewModel()
    {
        DataContext = null;
    }

    void PopupButton_BeginEdit(object sender, EventArgs e)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
    }

    void PopupButton_EndEdit(object sender, EventArgs e)
    {
        EndEdit?.Invoke(this, EventArgs.Empty);
    }
}
