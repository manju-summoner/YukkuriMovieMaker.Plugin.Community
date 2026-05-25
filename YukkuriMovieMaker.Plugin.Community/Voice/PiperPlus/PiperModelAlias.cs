using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperModelAlias : Bindable
{
    string modelPath = string.Empty;
    string displayName = string.Empty;

    public string ModelPath
    {
        get => modelPath;
        set => Set(ref modelPath, value);
    }

    public string DisplayName
    {
        get => displayName;
        set => Set(ref displayName, value);
    }

    public PiperModelAlias() { }

    public PiperModelAlias(string modelPath, string displayName)
    {
        this.modelPath = modelPath;
        this.displayName = displayName;
    }
}
