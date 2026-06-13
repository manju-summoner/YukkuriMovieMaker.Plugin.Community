using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    public class GrokTTSSettingsViewCommands
    {
        public static ICommand DeleteVoice { get; } = new ActionCommand(
            x => x is GrokTTSVoice,
            x =>
            {
                if (x is not GrokTTSVoice voice)
                    return;
                GrokTTSSettings.Default.Voices.Remove(voice);
            });
    }
}
