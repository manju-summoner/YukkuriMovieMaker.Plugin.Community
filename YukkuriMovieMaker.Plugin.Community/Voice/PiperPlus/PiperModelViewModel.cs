using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperModelViewModel(PiperSavedModel saved) : Bindable
{
    string displayName = PiperPlusSettings.Default.ResolveDisplayName(saved.ModelPath, saved.ModelName);

    public string ModelName => saved.ModelName;
    public string ModelPath => saved.ModelPath;
    public int NumSpeakers => saved.NumSpeakers;
    public string LanguageCodes => string.Join(", ", saved.LanguageCodes);

    public string DisplayName
    {
        get => displayName;
        set
        {
            if (Set(ref displayName, value))
                PiperPlusSettings.Default.SetDisplayName(saved.ModelPath, value);
        }
    }
}
