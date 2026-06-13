using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperModelViewModel(PiperModelInfo model)
{
    public string ModelName => model.ModelName;
    public string ModelPath => model.ModelPath;
    public int NumSpeakers => model.NumSpeakers;
    public string LanguageCodes => string.Join(", ", model.LanguageCodes);
}
