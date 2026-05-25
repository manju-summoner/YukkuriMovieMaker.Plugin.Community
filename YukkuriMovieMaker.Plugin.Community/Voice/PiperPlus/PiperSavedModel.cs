using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperSavedModel : Bindable
{
    string modelPath = string.Empty;
    string configPath = string.Empty;
    string modelName = string.Empty;
    int numSpeakers;
    string languageArgument = string.Empty;

    public string ModelPath
    {
        get => modelPath;
        set => Set(ref modelPath, value);
    }

    public string ConfigPath
    {
        get => configPath;
        set => Set(ref configPath, value);
    }

    public string ModelName
    {
        get => modelName;
        set => Set(ref modelName, value);
    }

    public int NumSpeakers
    {
        get => numSpeakers;
        set => Set(ref numSpeakers, value);
    }

    public string LanguageArgument
    {
        get => languageArgument;
        set => Set(ref languageArgument, value);
    }

    public ObservableCollection<string> LanguageCodes { get; } = [];

    public PiperSavedModel() { }
}
