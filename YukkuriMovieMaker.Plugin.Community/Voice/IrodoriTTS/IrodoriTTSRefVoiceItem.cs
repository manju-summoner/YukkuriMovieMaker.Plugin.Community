using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSRefVoiceItem : Bindable
{
    public string FilePath { get; set; } = string.Empty;
    public string Name { get; set => Set(ref field, value); } = string.Empty;
    public string Caption { get; set => Set(ref field, value); } = string.Empty;
    public string SourceApplication { get; set => Set(ref field, value); } = string.Empty;
}
