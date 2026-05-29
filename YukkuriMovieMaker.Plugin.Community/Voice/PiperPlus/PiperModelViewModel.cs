namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperModelViewModel(PiperSavedModel saved)
{
    public string ModelName => saved.ModelName;
    public string ModelPath => saved.ModelPath;
    public int NumSpeakers => saved.NumSpeakers;
    public string LanguageCodes => string.Join(", ", saved.LanguageCodes);
}
