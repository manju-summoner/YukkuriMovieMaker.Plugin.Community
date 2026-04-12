using System.Collections.Generic;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;

internal class IrodoriTTSEmojiPaletteViewModel : Bindable
{
    readonly IVoiceItemEditService? voiceItemEdit;

    public IReadOnlyList<EmojiItem> Emojis { get; } = IrodoriTTSEmojiDefinitions.All;

    public ICommand InsertEmojiCommand { get; }

    public IrodoriTTSEmojiPaletteViewModel(IVoiceItemEditService? voiceItemEdit)
    {
        this.voiceItemEdit = voiceItemEdit;

        InsertEmojiCommand = new ActionCommand(
            _ => voiceItemEdit is not null,
            param =>
            {
                if (param is not EmojiItem emoji || voiceItemEdit is null)
                    return;
                voiceItemEdit.Hatsuon = (voiceItemEdit.Hatsuon ?? "") + emoji.Emoji;
            });
    }
}
