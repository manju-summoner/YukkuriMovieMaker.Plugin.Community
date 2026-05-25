using System.Collections.ObjectModel;
using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettings : SettingsBase<PiperPlusSettings>
{
    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "Piper Plus";
    public override bool HasSettingView => true;
    public override object? SettingView => new PiperPlusSettingsView();

    string modelDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PiperPlusModels");

    string binaryDirectory = Path.Combine(
        AppDirectories.UserResourceDirectory, "piper");

    public string ModelDirectory
    {
        get => modelDirectory;
        set => Set(ref modelDirectory, value);
    }

    public string BinaryDirectory
    {
        get => binaryDirectory;
        set
        {
            if (Set(ref binaryDirectory, value))
                Resource.PiperBinaryResource.InvalidateCache();
        }
    }

    public ObservableCollection<PiperSpeakerEntry> Speakers { get; } = [];

    public override void Initialize()
    {
    }
}
