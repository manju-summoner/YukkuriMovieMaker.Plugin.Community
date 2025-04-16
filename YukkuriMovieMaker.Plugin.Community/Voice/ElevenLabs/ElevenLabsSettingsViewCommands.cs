using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    public class ElevenLabsSettingsViewCommands
    {
        public static ICommand DeleteVoice { get; } = new ActionCommand(
            x => x is ElevenLabsVoice,
            x => 
            {
                if (x is not ElevenLabsVoice voice)
                    return;
                ElevenLabsSettings.Default.Voices.Remove(voice);
            });
    }
}
