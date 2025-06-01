using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Zoom
{
    internal class AudioVolumeZoomEffectProcessor(IGraphicsDevicesAndContext devices, AudioVolumeZoomEffect item) : AudioVolumeVideoEffectProcessorBase(devices, item)
    {
        public override DrawDescription Update(EffectDescription effectDescription, double audioVolume)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var zoom = (float)(item.Zoom.GetValue(frame, length, fps) / 100.0);
            var zoomX = (float)(item.ZoomX.GetValue(frame, length, fps) / 100.0);
            var zoomY = (float)(item.ZoomY.GetValue(frame, length, fps) / 100.0);

            var drawDescription = effectDescription.DrawDescription;
            return drawDescription with
            {
                Zoom = drawDescription.Zoom * (new Vector2(zoomY, zoomX) * zoom * (float)audioVolume + new Vector2(1, 1) * (1 - (float)audioVolume))
            };
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
