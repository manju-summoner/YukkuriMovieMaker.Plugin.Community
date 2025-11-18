using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    public class VoiSonaTalkVoicePronounce : UndoRedoable, IVoicePronounce
    {
        string? tsml;
        [Display(Name = nameof(Texts.Intonation), Description = nameof(Texts.Intonation), ResourceType = typeof(Texts))]
        [VoiSonaTalkEditor]
        public string? TSML { get => tsml; set => Set(ref tsml, value); }
        
        public void BeginEdit()
        {

        }

        public ValueTask EndEditAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
