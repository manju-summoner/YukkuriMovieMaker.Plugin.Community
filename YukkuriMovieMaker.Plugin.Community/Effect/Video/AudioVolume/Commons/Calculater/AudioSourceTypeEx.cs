using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater
{
    public static class AudioSourceTypeEx
    {
        public static AudioSourceReaderParameterBase Convert(this AudioSourceType type, AudioSourceReaderParameterBase current)
        {
            var store = current.GetSharedData();
            AudioSourceReaderParameterBase param = type switch
            {
                AudioSourceType.File => new FileAudioSourceReaderParameter(store),
                AudioSourceType.Scene => new SceneAudioSourceReaderParameter(store),
                AudioSourceType.Timeline => new TimelineAudioSourceReaderParameter(store),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
            return param.GetType() != current.GetType() ? param : current;
        }
    }
}
