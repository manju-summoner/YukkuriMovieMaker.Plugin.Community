using System.IO;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusVoicePlugin : IVoicePlugin
{
    public string Name => "Piper Plus";

    public IEnumerable<IVoiceSpeaker> Voices => GetVoices();

    public bool CanUpdateVoices => true;

    public bool IsVoicesCached => PiperPlusSettings.Default.Speakers.Count > 0;

    public Task UpdateVoicesAsync() => Task.Run(PiperSpeakerLoader.Reload);

    static IEnumerable<IVoiceSpeaker> GetVoices()
    {
        var speakers = PiperPlusSettings.Default.Speakers.ToList();
        foreach (var entry in speakers)
            yield return new PiperPlusVoiceSpeaker(entry);

        var downloadedModelPaths = speakers
            .Select(s => s.ModelPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in PretrainedModelCatalog.All)
        {
            if (downloadedModelPaths.Contains(definition.ModelPath))
                continue;

            yield return new PiperPlusVoiceSpeaker(
                new PiperSpeakerEntry
                {
                    ModelPath = definition.ModelPath,
                    ConfigPath = definition.ConfigPath,
                    ModelName = Path.GetFileNameWithoutExtension(definition.OnnxFileName),
                    ModelDisplayName = definition.DisplayName,
                    SpeakerId = 0,
                    SpeakerName = definition.DisplayName,
                    IsMultiSpeaker = false,
                    LanguageArgument = string.Empty,
                },
                new PretrainedModelResource(definition));
        }
    }
}
