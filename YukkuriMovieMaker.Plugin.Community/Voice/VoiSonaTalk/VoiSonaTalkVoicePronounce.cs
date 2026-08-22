using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
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

        public LipSyncFrame[]? LipSyncFrames { get; set => Set(ref field, value); }

        public string[]? Phonemes { get; set => Set(ref field, value); }

        public double[]? PhonemeDurations { get; set => Set(ref field, value); }

        // ユーザーが指定した音素長（負値=自動）。実測値のPhonemeDurationsを指定長として送り返すと
        // 全音素が固定されて話速などのパラメーターが効かなくなるため、指定長は実測値と分けて保持する
        public double[]? RequestedPhonemeDurations { get; set => Set(ref field, value); }

        public void BeginEdit()
        {

        }

        public ValueTask EndEditAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
