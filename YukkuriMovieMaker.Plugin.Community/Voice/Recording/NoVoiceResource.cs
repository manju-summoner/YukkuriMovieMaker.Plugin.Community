using System;
using System.ComponentModel;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public class NoVoiceResource : IVoiceResource
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string Name => "録音ツール";
        public string Terms => string.Empty;
        public bool IsDownloaded => true;
        public string FileSize => string.Empty;

        public event EventHandler? DownloadStarted
        {
            add { }
            remove { }
        }

        public Task DownloadAsync(ProgressMessage progress) => Task.CompletedTask;

        public Task<bool> HasUpdateAsync() => Task.FromResult(false);
    }
}


