using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FishEyeLens
{
    internal class FishEyeLensProcessor(IGraphicsDevicesAndContext devices, FishEyeLensEffect fisyEyeLensEffect) : VideoEffectProcessorBase(devices)
    {
        FishEyeLensCustomEffect? effect;

        bool isFirst = true;
        ProjectionMode projection;
        double angle, zoom;
        RawRectF bounds;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (effect is null || IsPassThroughEffect)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var projection = fisyEyeLensEffect.Projection;
            var angle = Math.Clamp(fisyEyeLensEffect.Angle.GetValue(frame, length, fps), -179.9, 179.99) / 180 * Math.PI / 2;
            var zoom = Math.Max(fisyEyeLensEffect.Zoom.GetValue(frame, length, fps) / 100, 0.0001);
            var bounds = devices.DeviceContext.GetImageLocalBounds(input);

            if(isFirst || this.projection != projection)
                effect.Projection = projection;
            if(isFirst || this.angle != angle)
                effect.Angle = (float)angle;
            if(isFirst || this.zoom != zoom)
                effect.Zoom = (float)zoom;
            if(isFirst || !this.bounds.Equals(bounds))
                effect.Rect = new System.Numerics.Vector4(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

            isFirst = false;
            this.projection = projection;
            this.angle = angle;
            this.zoom = zoom;
            this.bounds = bounds;
            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new FishEyeLensCustomEffect(devices);
            if(!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);

        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }
    }
}