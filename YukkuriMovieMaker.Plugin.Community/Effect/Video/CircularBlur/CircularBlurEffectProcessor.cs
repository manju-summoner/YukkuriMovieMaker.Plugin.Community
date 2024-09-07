using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CircularBlur
{
    public class CircularBlurEffectProcessor(IGraphicsDevicesAndContext devices, CircularBlurEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirst = true;
        double angle, x, y;
        bool isHardBorder;

        CircularBlurCustomEffect? effect;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var angle = item.Angle.GetValue(frame, length, fps);
            var x = item.X.GetValue(frame, length, fps);
            var y = item.Y.GetValue(frame, length, fps);
            var isHardBorder = item.IsHardBorderMode;

            if (isFirst || this.angle != angle)
                effect.Angle = (float)angle;
            if (isFirst || this.x != x)
                effect.X = (float)x;
            if (isFirst || this.y != y)
                effect.Y = (float)y;
            if (isFirst || this.isHardBorder != isHardBorder)
                effect.IsHardBorder = isHardBorder;

            var control =
                new VideoEffectController(
                    item,
                    [
                        new ControllerPoint(
                            new((float)x, (float)y, 0f),
                            x=>
                            {
                                item.X.AddToEachValues(x.Delta.X);
                                item.Y.AddToEachValues(x.Delta.Y);
                            })
                    ]);

            isFirst = false;
            this.angle = angle;
            this.x = x;
            this.y = y;
            this.isHardBorder = isHardBorder;

            return effectDescription.DrawDescription with 
            { 
                Controllers = 
                [
                    ..effectDescription.DrawDescription.Controllers,
                    control
                ],
            };
        }

        protected override void ClearEffectChain() => effect?.SetInput(0, null, true);

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new CircularBlurCustomEffect(devices);
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