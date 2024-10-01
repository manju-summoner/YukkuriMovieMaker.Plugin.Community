using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using D2D = Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorShift
{
    public class ColorShiftEffectProcessor(IGraphicsDevicesAndContext devices, ColorShiftEffect item) : VideoEffectProcessorBase(devices)
    {
        ColorShiftCustomEffect? effect;

        bool isFirst = true;
        double shift, angle, strength;
        int mode;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var shift = item.Shift.GetValue(frame, length, fps);
            var angle = item.Angle.GetValue(frame, length, fps);
            var strength = item.Strength.GetValue(frame, length, fps);
            var isPremultiplied = item.IsPremultipliedAlpha;
            var mode = (int)item.Mode * (isPremultiplied ? -1 : 1);

            if (isFirst || this.shift != shift)
                effect.Distance = (float)shift;
            if (isFirst || this.angle != angle)
                effect.Angle = (float)angle;
            if (isFirst || this.strength != strength)
                effect.Strength = (float)(strength / 100d);
            if (isFirst || this.mode != mode)
                effect.Mode = mode;

            isFirst = false;
            this.shift = shift;
            this.angle = angle;
            this.strength = strength;
            this.mode = mode;
            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            SetInput(null);
        }

        protected override D2D.ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new(devices);
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

        protected override void setInput(D2D.ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }
    }
}
