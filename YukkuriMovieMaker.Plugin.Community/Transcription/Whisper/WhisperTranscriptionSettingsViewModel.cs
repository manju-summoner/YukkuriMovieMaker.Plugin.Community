using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    internal class WhisperTranscriptionSettingsViewModel : Bindable
    {
        public IReadOnlyList<WhisperModel> Models { get; } = WhisperModels.GetDefaultAndUserModels();
    }
}
