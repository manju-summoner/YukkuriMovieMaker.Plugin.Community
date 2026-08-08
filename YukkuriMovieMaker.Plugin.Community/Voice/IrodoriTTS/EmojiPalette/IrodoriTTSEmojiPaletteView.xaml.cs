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
        // EditorInfoはプレビュー再生や履歴変化のたびに更新されるため、VMは初回のみ生成して以降は差し替えのみ行う
        if (DataContext is IrodoriTTSEmojiPaletteViewModel currentVm)
        {
            currentVm.SetEditorInfo(info);
            return;
        }

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
        if (DataContext is IrodoriTTSEmojiPaletteViewModel vm)
            vm.Close();
        DataContext = null;
    }

    void PopupButton_BeginEdit(object sender, EventArgs e)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
    }

    void PopupButton_EndEdit(object sender, EventArgs e)
    {
        // ポップアップを閉じたら再生を停止する（VoiSonaTalkEditorと同じ挙動）
        if (DataContext is IrodoriTTSEmojiPaletteViewModel vm)
            vm.StopPlayback();
        EndEdit?.Invoke(this, EventArgs.Empty);
    }
}
