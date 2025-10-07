using YukkuriMovieMaker.Plugin.VoiceActivityDetector;

namespace YukkuriMovieMaker.Plugin.Community.VoiceActivityDetector.SileroVad
{
    internal class SileroVoiceDetectorPlugin : IVoiceActivityDetectorPlugin
    {
        public string Name => "SileroVAD";

        public IVoiceActivityDetectorParameter CreateParameter()
        {
            return new SileroVoiceDetectorParameter();
        }
    }
}
