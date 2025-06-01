using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase
{
    public abstract class AudioVolumeVideoEffectProcessorBase : VideoEffectProcessorBase
    {
        protected readonly AudioVolumeCalculaterSource calculaterSource;
        
        public AudioVolumeVideoEffectProcessorBase(IGraphicsDevicesAndContext devices, AudioVolumeVideoEffectBase item) : base(devices)
        {
            calculaterSource = item.Calculater.CreateSource();
            disposer.Collect(calculaterSource);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            return Update(effectDescription, calculaterSource.GetVolume(effectDescription));
        }

        public abstract DrawDescription Update(EffectDescription effectDescription, double audioVolume);
    }
}
