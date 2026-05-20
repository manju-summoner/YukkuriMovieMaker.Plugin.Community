using System.Collections.Generic;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Update;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class RecordedVoicePlugin : IVoicePlugin
    {
        static RecordedVoicePlugin()
        {
        }

        public string Name => "録音ツール";
        public PluginDetailsAttribute Details => new PluginDetailsAttribute
        {
            AuthorName = "CommunityRecording",
            ContentId = "CommunityRecording"
        };
        public IPluginUpdater? Updater => null;

        public IEnumerable<IVoiceSpeaker> Voices => new[] { RecordedVoiceSpeaker.Instance };
        public bool CanUpdateVoices => false;
        public bool IsVoicesCached => true;

        public Task UpdateVoicesAsync() => Task.CompletedTask;
    }
}



