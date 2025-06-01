using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public abstract class AudioSourceReaderParameterBase : SharedParameterBase
    {
        public AudioSourceReaderParameterBase() : base() { }

        public AudioSourceReaderParameterBase(SharedDataStore? store = null) : base(store) { }

        public abstract AudioSourceReaderBase CreateReader();
    }
}
