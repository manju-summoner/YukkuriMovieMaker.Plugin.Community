using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

public class IrodoriTTSVoicePronounce : UndoRedoable, IVoicePronounce
{
    [Display(Name = " ")]
    [JsonIgnore]
    [IrodoriTTSEmojiPaletteEditor]
    public object EditorDummy { get; } = new();

    [JsonIgnore]
    public object Dummy { get => field; set => Set(ref field, value); } = new();

    public void RaiseChanged() => Dummy = new object();

    public void BeginEdit() { }
    public ValueTask EndEditAsync() => ValueTask.CompletedTask;
}
