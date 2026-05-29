using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettings : SettingsBase<PiperPlusSettings>
{
    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "Piper Plus";
    public override bool HasSettingView => true;
    public override object? SettingView => new PiperPlusSettingsView();

    public ObservableCollection<PiperSpeakerEntry> Speakers { get; } = [];

    public ObservableCollection<PiperModelAlias> ModelAliases { get; } = [];

    public ObservableCollection<PiperSavedModel> SavedModels { get; } = [];

    public string ResolveDisplayName(string modelPath, string fallback)
    {
        var alias = ModelAliases.FirstOrDefault(a => a.ModelPath == modelPath);
        return alias is { DisplayName: { Length: > 0 } name }
            ? name
            : fallback;
    }

    public void SetDisplayName(string modelPath, string displayName)
    {
        var alias = ModelAliases.FirstOrDefault(a => a.ModelPath == modelPath);
        if (alias is not null)
            alias.DisplayName = displayName;
        else
            ModelAliases.Add(new PiperModelAlias(modelPath, displayName));
    }

    public void PruneAliases(IEnumerable<string> activeModelPaths)
    {
        var active = new HashSet<string>(activeModelPaths);
        var stale = ModelAliases.Where(a => !active.Contains(a.ModelPath)).ToList();
        foreach (var a in stale)
            ModelAliases.Remove(a);
    }

    public override void Initialize()
    {
    }
}
