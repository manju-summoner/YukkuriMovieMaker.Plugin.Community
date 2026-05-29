using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusVoicePlugin : IVoicePlugin
{
    public string Name => "Piper Plus";

    public IEnumerable<IVoiceSpeaker> Voices => GetVoices();

    public bool CanUpdateVoices => true;

    public bool IsVoicesCached => PiperSpeakerLoader.IsLoaded;

    public Task UpdateVoicesAsync() => Task.Run(PiperSpeakerLoader.Reload);

    static IEnumerable<IVoiceSpeaker> GetVoices()
    {
        if (!PiperSpeakerLoader.IsLoaded)
            PiperSpeakerLoader.Reload();

        var speakers = PiperSpeakerLoader.Speakers.ToList();
        foreach (var entry in speakers)
            yield return new PiperPlusVoiceSpeaker(entry, new PiperBinaryVoiceResource());

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
                    SpeakerId = 0,
                    SpeakerName = definition.ModelName,
                    IsMultiSpeaker = false,
                },
                new PretrainedModelResource(definition));
        }
    }
}
