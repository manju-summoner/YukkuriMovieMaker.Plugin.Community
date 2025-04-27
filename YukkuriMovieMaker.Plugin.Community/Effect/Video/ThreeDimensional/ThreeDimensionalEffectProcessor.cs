using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ThreeDimensional
{
    public class ThreeDimensionalEffectProcessor(IGraphicsDevicesAndContext devices, ThreeDimensionalEffect item) : VideoEffectProcessorBase(devices)
    {
        ThreeDimensionalProcessor? effect;
        bool isFirst = true;
        double x,
               y,
               shadowLength,
               opacity,
               attenuation;
        Color color1;
        Color color2;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var x = item.X.GetValue(frame, length, fps);
            var y = item.Y.GetValue(frame, length, fps);
            var shadowLength = item.Length.GetValue(frame, length, fps);
            var opacity = item.Opacity.GetValue(frame, length, fps);
            var attenuation = item.Attenuation.GetValue(frame, length, fps);
            var color1 = item.ShadowType is ThreeDimensionalType.Image ? Colors.Transparent : item.Color1;
            var color2 = item.ShadowType switch
            {
                ThreeDimensionalType.Image => Colors.Transparent,
                ThreeDimensionalType.Solid => item.Color1,
                ThreeDimensionalType.Gradient => item.Color2,
                _ => throw new NotImplementedException()
            };
            if (item.IsAbsolutePoint)
            {
                x -= effectDescription.DrawDescription.DrawPoint.X;
                y -= effectDescription.DrawDescription.DrawPoint.Y;
            }
            if (isFirst || this.x != x || this.y != y || this.shadowLength != shadowLength || this.opacity != opacity || this.attenuation != attenuation || this.color1 != color1 || this.color2 != color2)
            {
                effect.Center = new System.Numerics.Vector2((float)x, (float)y);
                effect.Length = (float)shadowLength / 100;
                effect.Opacity = (float)opacity / 100;
                effect.Attenuation = (float)attenuation / 100;
                effect.Color1 = new System.Numerics.Vector4(
                    (float)color1.R / 255 * color1.A / 255,
                    (float)color1.G / 255 * color1.A / 255,
                    (float)color1.B / 255 * color1.A / 255,
                    (float)color1.A / 255);
                effect.Color2 = new System.Numerics.Vector4(
                    (float)color2.R / 255 * color2.A / 255,
                    (float)color2.G / 255 * color2.A / 255,
                    (float)color2.B / 255 * color2.A / 255,
                    (float)color2.A / 255);
            }

            isFirst = false;
            this.x = x;
            this.y = y;
            this.shadowLength = shadowLength;
            this.opacity = opacity;
            this.attenuation = attenuation;
            this.color1 = color1;
            this.color2 = color2;
            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new ThreeDimensionalProcessor(devices);
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
    }
}