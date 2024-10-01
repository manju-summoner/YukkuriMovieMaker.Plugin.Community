using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Ripple
{
    public class RippleEffectProcessor(IGraphicsDevicesAndContext devices, RippleEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirstUpdate = true;
        double x;
        double y;
        double waveLength;
        double amplitude;
        double phase;

        RippleCustomEffect? rippleEffect;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || rippleEffect is null) return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var time = effectDescription.ItemPosition.Time;

            var x = item.X.GetValue(frame, length, fps);
            var y = item.Y.GetValue(frame, length, fps);
            var waveLength = item.WaveLength.GetValue(frame, length, fps);
            var amplitude = item.Amplitude.GetValue(frame, length, fps);
            var period = item.Period.GetValue(frame, length, fps);
            var phase = period is 0 ? 0 : -time.TotalSeconds / period * 2 * Math.PI;

            if (isFirstUpdate || this.x != x)
                rippleEffect.X = (float)x;

            if (isFirstUpdate || this.y != y)
                rippleEffect.Y = (float)y;

            if (isFirstUpdate || this.waveLength != waveLength)
                rippleEffect.WaveLength = (float)waveLength;

            if (isFirstUpdate || this.amplitude != amplitude)
                rippleEffect.Amplitude = (float)amplitude;

            if (isFirstUpdate || this.phase != phase)
                rippleEffect.Phase = (float)phase;

            isFirstUpdate = false;
            this.x = x;
            this.y = y;
            this.waveLength = waveLength;
            this.amplitude = amplitude;
            this.phase = phase;

            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            SetInput(null);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            rippleEffect = new(devices);
            if (!rippleEffect.IsEnabled)
            {
                rippleEffect.Dispose();
                rippleEffect = null;
                return null;
            }
            disposer.Collect(rippleEffect);

            var output = rippleEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            rippleEffect?.SetInput(0, input, true);
        }
    }
}
