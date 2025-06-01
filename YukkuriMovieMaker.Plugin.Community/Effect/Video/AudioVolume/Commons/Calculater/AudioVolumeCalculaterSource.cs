using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public class AudioVolumeCalculaterSource(AudioVolumeCalculater item) : IDisposable
    {
        readonly DisposeCollector disposer = new();

        AudioSourceReaderBase? reader;
        bool isFirst = true;
        AudioSourceType sourceType;

        public double GetVolume(TimelineItemSourceDescription timelineItemSourceDescription)
        {
            var frame = timelineItemSourceDescription.ItemPosition.Frame;
            var length = timelineItemSourceDescription.ItemDuration.Frame;
            var fps = timelineItemSourceDescription.FPS;

            var sourceType = item.SourceType;
            var sourceParameter = item.SourceParameter;
            var power = item.Power.GetValue(frame, length, fps) / 100;
            var smooth = (int)item.Smooth.GetValue(frame, length, fps);
            var playback = item.Playback;

            if (isFirst || this.sourceType != sourceType)
            {
                if (reader is not null)
                {
                    disposer.RemoveAndDispose(ref reader);
                    reader = null;
                }
                reader = sourceParameter.CreateReader();
                disposer.Collect(reader);
            }

            (int size, int count, float[] destBuffer) = reader?.Read(timelineItemSourceDescription, smooth, playback) ?? (0, 0, []);

            isFirst = false;
            this.sourceType = sourceType;

            if (count == 0)
                return 0;
            double volume = 0;
            for (int i = 0; i < size; i++)
                volume += Math.Min(1, destBuffer[i] * power * destBuffer[i] * power);
            return Math.Sqrt(volume / count);
        }

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
