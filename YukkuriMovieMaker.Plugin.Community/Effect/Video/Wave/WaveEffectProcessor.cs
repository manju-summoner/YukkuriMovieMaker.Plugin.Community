using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Wave
{
    public class WaveEffectProcessor(IGraphicsDevicesAndContext devices, WaveEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirstUpdate = true;
        double angle1;
        double angle2;
        double amplitude;
        double waveLength;
        double phase;

        WaveCustomEffect? waveEffect;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            waveEffect = new(devices);
            if (!waveEffect.IsEnabled)
            {
                waveEffect.Dispose();
                waveEffect = null;
                return null;
            }
            disposer.Collect(waveEffect);

            var output = waveEffect.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || waveEffect is null) return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var time = effectDescription.ItemPosition.Time;

            var angle1 = item.Angle1.GetValue(frame, length, fps) / 180 * Math.PI;
            var angle2 = item.Angle2.GetValue(frame, length, fps) / 180 * Math.PI;
            var amplitude = item.Amplitude.GetValue(frame, length, fps);
            var waveLength = item.WaveLength.GetValue(frame, length, fps);
            var period = item.Period.GetValue(frame, length, fps);
            var phase = period is 0 ? 0 : time.TotalSeconds / period;

            if (isFirstUpdate || this.angle1 != angle1)
                waveEffect.Angle1 = (float)angle1;
            if (isFirstUpdate || this.angle2 != angle2)
                waveEffect.Angle2 = (float)angle2;
            if (isFirstUpdate || this.amplitude != amplitude)
                waveEffect.Amplitude = (float)amplitude;
            if (isFirstUpdate || this.waveLength != waveLength)
                waveEffect.Length = (float)waveLength;
            if (isFirstUpdate || this.phase != phase)
                waveEffect.Phase = (float)phase;

            isFirstUpdate = false;
            this.angle1 = angle1;
            this.angle2 = angle2;
            this.amplitude = amplitude;
            this.waveLength = waveLength;
            this.phase = phase;

            return effectDescription.DrawDescription;
        }

        protected override void setInput(ID2D1Image? input)
        {
            waveEffect?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain()
        {
            SetInput(null);
        }

    }
}
