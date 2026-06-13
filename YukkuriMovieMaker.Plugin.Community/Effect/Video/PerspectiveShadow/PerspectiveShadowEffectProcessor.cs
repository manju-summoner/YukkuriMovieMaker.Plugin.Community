using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PerspectiveShadow
{
    internal sealed class PerspectiveShadowEffectProcessor(IGraphicsDevicesAndContext devices, PerspectiveShadowEffect item)
        : VideoEffectProcessorBase(devices)
    {
        private PerspectiveShadowCustomEffect? effect;

        private bool isFirst = true;
        private double lightX, lightY, lightHeight, groundY;
        private double opacity, falloff;
        private double blurRadius, spread, alphaThreshold;
        private Color shadowColor;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var lightX = item.LightX.GetValue(frame, length, fps);
            var lightY = item.LightY.GetValue(frame, length, fps);
            var lightHeight = item.LightHeight.GetValue(frame, length, fps);
            var groundY = item.GroundY.GetValue(frame, length, fps);
            var opacity = item.Opacity.GetValue(frame, length, fps);
            var falloff = item.Falloff.GetValue(frame, length, fps);
            var blurRadius = item.BlurRadius.GetValue(frame, length, fps);
            var spread = item.Spread.GetValue(frame, length, fps);
            var alphaThreshold = item.AlphaThreshold.GetValue(frame, length, fps);
            var shadowColor = item.ShadowColor;

            if (isFirst
                || this.lightX != lightX
                || this.lightY != lightY
                || this.lightHeight != lightHeight
                || this.groundY != groundY
                || this.opacity != opacity
                || this.falloff != falloff
                || this.blurRadius != blurRadius
                || this.spread != spread
                || this.alphaThreshold != alphaThreshold
                || this.shadowColor != shadowColor)
            {
                effect.LightX = (float)lightX;
                effect.LightY = (float)lightY;
                effect.LightHeight = (float)lightHeight;
                effect.GroundY = (float)groundY;
                effect.Opacity = (float)opacity / 100f;
                effect.Falloff = (float)falloff / 100f;
                effect.BlurRadius = (float)blurRadius;
                effect.Spread = (float)spread;
                effect.AlphaThreshold = (float)alphaThreshold;
                effect.ShadowColor = new Vector4(
                    shadowColor.R / 255f,
                    shadowColor.G / 255f,
                    shadowColor.B / 255f,
                    shadowColor.A / 255f);
            }

            isFirst = false;
            this.lightX = lightX;
            this.lightY = lightY;
            this.lightHeight = lightHeight;
            this.groundY = groundY;
            this.opacity = opacity;
            this.falloff = falloff;
            this.blurRadius = blurRadius;
            this.spread = spread;
            this.alphaThreshold = alphaThreshold;
            this.shadowColor = shadowColor;

            var controller = new VideoEffectController(
                item,
                [
                    new ControllerPoint(
                        new((float)lightX, (float)lightY, 0f),
                        arg =>
                        {
                            item.LightX.AddToEachValues(arg.Delta.X);
                            item.LightY.AddToEachValues(arg.Delta.Y);
                        })
                ]);

            var desc = effectDescription.DrawDescription;
            return desc with { Controllers = [..desc.Controllers, controller] };
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new PerspectiveShadowCustomEffect(devices);
            if (!effect.IsEnabled)
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
