using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Move
{
    internal class AudioVolumeMoveEffectProcessor(IGraphicsDevicesAndContext devices, AudioVolumeMoveEffect item) : AudioVolumeVideoEffectProcessorBase(devices, item)
    {
        public override DrawDescription Update(EffectDescription effectDescription, double audioVolume)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var x = (float)(item.X.GetValue(frame, length, fps) * audioVolume);
            var y = (float)(item.Y.GetValue(frame, length, fps) * audioVolume);
            var z = (float)(item.Z.GetValue(frame, length, fps) * audioVolume);

            var drawDescription = effectDescription.DrawDescription;

            return drawDescription with { Draw = drawDescription.Draw + new Vector3(x, y, z) };
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
