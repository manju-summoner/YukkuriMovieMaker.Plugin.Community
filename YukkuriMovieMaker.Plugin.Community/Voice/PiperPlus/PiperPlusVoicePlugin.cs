using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusVoicePlugin : IVoicePlugin
{
    public string Name => "Piper Plus";

    public IEnumerable<IVoiceSpeaker> Voices =>
        PiperPlusSettings.Default.Speakers
            .Select(entry => new PiperPlusVoiceSpeaker(entry));

    public bool CanUpdateVoices => true;

    public bool IsVoicesCached => PiperPlusSettings.Default.Speakers.Count > 0;

    public Task UpdateVoicesAsync() => Task.Run(PiperSpeakerLoader.Reload);
}
