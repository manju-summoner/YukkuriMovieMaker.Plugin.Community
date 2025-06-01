using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader
{
    public class SceneAudioSourceReader(SceneAudioSourceReaderParameter item) : AudioSourceReaderBase
    {
        IAudioStream? source;
        bool isFirst = true;
        Guid sceneId;

        public override (int, int, float[]) Read(TimelineItemSourceDescription timelineItemSourceDescription, int smooth, TimeSpan playback)
        {
            var fps = timelineItemSourceDescription.FPS;
            var sceneId = item.SceneId;
            bool isSourceChanged = isFirst || this.sceneId != sceneId;

            if (isSourceChanged)
            {
                if (source is not null)
                    disposer.RemoveAndDispose(ref source);
                timelineItemSourceDescription.Scenes.FirstOrDefault(x => x.ID == sceneId)?.TryCreateAudioSource(out source);
                if (source is not null)
                    disposer.Collect(source);
            }

            int samplingRate = source is null ? 0 : source.Hz;
            TimeSpan position = timelineItemSourceDescription.ItemPosition.Time + playback - TimeSpan.FromSeconds(1 + smooth).Divide(fps * 2);
            TimeSpan headCut, cutPosition;
            if (position < TimeSpan.Zero)
            {
                headCut = -position;
                cutPosition = TimeSpan.Zero;
            }
            else
            {
                headCut = TimeSpan.Zero;
                cutPosition = position;
            }
            int count = samplingRate * smooth / fps * 2;
            int cutCount = count - (int)(samplingRate * headCut.TotalSeconds) * 2;
            float[] destBuffer = new float[cutCount];

            source?.Seek(cutPosition);
            int size = source?.Read(destBuffer, 0, cutCount) ?? 0;

            isFirst = false;
            this.sceneId = sceneId;

            return (size, count, destBuffer);
        }
    }
}
