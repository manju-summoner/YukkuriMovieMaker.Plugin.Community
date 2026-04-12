using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSRefVoiceItem : Bindable
{
    public string FilePath { get; set; } = string.Empty;
    public string Name { get => field; set => Set(ref field, value); } = string.Empty;
    public string Caption { get => field; set => Set(ref field, value); } = string.Empty;
    public string SourceApplication { get => field; set => Set(ref field, value); } = string.Empty;
}
