using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperModelViewModel : Bindable
{
    readonly PiperSavedModel saved;
    string displayName;

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

    public PiperModelViewModel(PiperSavedModel saved)
    {
        this.saved = saved;
        displayName = PiperPlusSettings.Default.ResolveDisplayName(saved.ModelPath, saved.ModelName);
    }
}
