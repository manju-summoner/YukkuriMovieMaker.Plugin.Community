using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkVoicePronounce : UndoRedoable, IVoicePronounce
    {
        string? tsml;
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
