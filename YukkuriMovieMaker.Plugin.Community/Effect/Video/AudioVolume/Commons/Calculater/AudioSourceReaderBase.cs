using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public abstract class AudioSourceReaderBase : IDisposable
    {
        protected readonly DisposeCollector disposer = new();

        public abstract (int, int, float[]) Read(TimelineItemSourceDescription timelineItemSourceDescription, int smooth, TimeSpan playback);

        #region IDisposable Support
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposer.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
