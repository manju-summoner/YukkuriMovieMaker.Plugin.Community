using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperSpeakerLoader
{
    public static void Reload()
    {
        var models = PiperModelScanner.Scan(PiperPlusSettings.Default.ModelDirectory);
        var entries = models.SelectMany(PiperSpeakerEntryBuilder.Build).ToList();
        var activePaths = models.Select(m => m.ModelPath).ToList();

        Application.Current.Dispatcher.Invoke(() =>
        {
            SyncSavedModels(models);

            PiperPlusSettings.Default.Speakers.Clear();
            foreach (var entry in entries)
                PiperPlusSettings.Default.Speakers.Add(entry);

            PiperPlusSettings.Default.PruneAliases(activePaths);
        });
    }

    static void SyncSavedModels(IReadOnlyList<PiperModelInfo> models)
    {
        var saved = PiperPlusSettings.Default.SavedModels;

        var incoming = models.ToDictionary(m => m.ModelPath);
        var existing = saved.ToDictionary(s => s.ModelPath);

        foreach (var stale in existing.Keys.Except(incoming.Keys).ToList())
            saved.Remove(existing[stale]);

        foreach (var model in models)
        {
            if (existing.TryGetValue(model.ModelPath, out var s))
            {
                s.ConfigPath = model.ConfigPath;
                s.ModelName = model.ModelName;
                s.NumSpeakers = model.NumSpeakers;
                s.LanguageArgument = model.LanguageArgument;
                s.LanguageCodes.Clear();
                foreach (var code in model.LanguageCodes)
                    s.LanguageCodes.Add(code);
            }
            else
            {
                var entry = new PiperSavedModel
                {
                    ModelPath = model.ModelPath,
                    ConfigPath = model.ConfigPath,
                    ModelName = model.ModelName,
                    NumSpeakers = model.NumSpeakers,
                    LanguageArgument = model.LanguageArgument,
                };
                foreach (var code in model.LanguageCodes)
                    entry.LanguageCodes.Add(code);
                saved.Add(entry);
            }
        }
    }
}
