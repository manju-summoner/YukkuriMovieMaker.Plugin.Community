using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperSpeakerLoader
{
    public static IReadOnlyList<PiperModelInfo> Models { get; private set; } = [];

    public static IReadOnlyList<PiperSpeakerEntry> Speakers { get; private set; } = [];

    public static bool IsLoaded { get; private set; }

    public static void Reload()
    {
        var models = PiperModelScanner.Scan(PiperPlusPaths.ModelDirectory);
        var entries = models.SelectMany(PiperSpeakerEntryBuilder.Build).ToList();

        Models = models;
        Speakers = entries;
        IsLoaded = true;
    }
}
