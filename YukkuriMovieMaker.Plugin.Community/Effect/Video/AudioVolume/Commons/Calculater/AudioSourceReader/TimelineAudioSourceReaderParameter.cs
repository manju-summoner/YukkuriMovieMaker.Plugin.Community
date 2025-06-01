using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader
{
    public class TimelineAudioSourceReaderParameter : AudioSourceReaderParameterBase
    {
        public TimelineAudioSourceReaderParameter() : base()
        {
        }

        public TimelineAudioSourceReaderParameter(SharedDataStore? store = null) : base(store)
        { 
        }

        public override AudioSourceReaderBase CreateReader()
        {
            return new TimelineAudioSourceReader();
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];

        protected override void LoadSharedData(SharedDataStore store) 
        {
        }

        protected override void SaveSharedData(SharedDataStore store) 
        { 
        }
    }
}
