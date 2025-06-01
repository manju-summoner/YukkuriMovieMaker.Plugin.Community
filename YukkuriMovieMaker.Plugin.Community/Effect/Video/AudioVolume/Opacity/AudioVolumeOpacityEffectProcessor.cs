using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Opacity
{
    internal class AudioVolumeOpacityEffectProcessor(IGraphicsDevicesAndContext devices, AudioVolumeOpacityEffect item) : AudioVolumeVideoEffectProcessorBase(devices, item)
    {
        public override DrawDescription Update(EffectDescription effectDescription, double audioVolume)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var invert = item.Invert;
            var opacity = invert ? item.Opacity.GetValue(frame, length, fps) / 100.0 * (1.0 - audioVolume) + audioVolume : item.Opacity.GetValue(frame, length, fps) / 100.0 * audioVolume + 1.0 - audioVolume;

            var drawDescription = effectDescription.DrawDescription;

            return drawDescription with { Opacity = drawDescription.Opacity * opacity };
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            return null;
        }

        protected override void setInput(ID2D1Image? input)
        {
        }

        protected override void ClearEffectChain()
        {
        }
    }
}
