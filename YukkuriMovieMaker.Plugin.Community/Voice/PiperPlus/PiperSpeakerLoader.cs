using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperSpeakerLoader
{
    public static void Reload()
    {
        var models = PiperModelScanner.Scan(PiperPlusSettings.Default.ModelDirectory);
        var entries = models
            .SelectMany(PiperSpeakerEntryBuilder.Build)
            .ToList();

        Application.Current.Dispatcher.Invoke(() =>
        {
            PiperPlusSettings.Default.Speakers.Clear();
            foreach (var entry in entries)
                PiperPlusSettings.Default.Speakers.Add(entry);
        });
    }
}
