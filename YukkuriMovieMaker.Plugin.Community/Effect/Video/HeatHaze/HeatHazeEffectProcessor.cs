using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    internal sealed class HeatHazeEffectProcessor(IGraphicsDevicesAndContext devices, HeatHazeEffect item) : VideoEffectProcessorBase(devices)
    {
        HeatHazeCustomEffect? effect;

        bool isFirst = true;
        HeatHazeControlMode controlMode;
        double temperature, humidity;
        double strength, scale, flowSpeed, boilSpeed;
        double angle, chromaticAberration, blurStrength;
        bool enableBlur;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var totalSeconds = (double)frame / fps;

            var controlMode = item.ControlMode;
            var temperature = item.Temperature.GetValue(frame, length, fps);
            var humidity = item.Humidity.GetValue(frame, length, fps);
            var strength = item.Strength.GetValue(frame, length, fps);
            var scale = item.Scale.GetValue(frame, length, fps);
            var flowSpeed = item.FlowSpeed.GetValue(frame, length, fps);
            var boilSpeed = item.BoilSpeed.GetValue(frame, length, fps);
            var angle = item.Angle.GetValue(frame, length, fps);
            var chromaticAberration = item.ChromaticAberration.GetValue(frame, length, fps);
            var enableBlur = item.EnableBlur;
            var blurStrength = item.BlurStrength.GetValue(frame, length, fps);

            if (isFirst
                || this.controlMode != controlMode
                || this.temperature != temperature
                || this.humidity != humidity
                || this.strength != strength
                || this.scale != scale
                || this.flowSpeed != flowSpeed
                || this.boilSpeed != boilSpeed
                || this.angle != angle
                || this.chromaticAberration != chromaticAberration
                || this.enableBlur != enableBlur
                || this.blurStrength != blurStrength)
            {
                float finalStrength, finalScale, finalFlowSpeed, finalBoilSpeed;

                if (controlMode == HeatHazeControlMode.Automatic)
                {
                    var tempFactor = Math.Clamp(((float)temperature - 15f) / 35f, 0f, 1.5f);
                    var humidityFactor = 1f + (float)humidity / 100f * 0.5f;

                    finalStrength = tempFactor * humidityFactor * 0.5f;
                    finalScale = (1f + tempFactor) * 1.5f;
                    finalFlowSpeed = tempFactor * 0.2f;
                    finalBoilSpeed = tempFactor * 0.3f;
                }
                else
                {
                    finalStrength = (float)strength / 100f;
                    finalScale = 100f / Math.Max((float)scale, 0.001f);
                    finalFlowSpeed = (float)flowSpeed / 100f;
                    finalBoilSpeed = (float)boilSpeed / 100f;
                }

                effect.Strength = finalStrength;
                effect.NoiseScale = finalScale;
                effect.FlowSpeed = finalFlowSpeed;
                effect.BoilSpeed = finalBoilSpeed;
                effect.Angle = (float)(angle * Math.PI / 180.0);
                effect.ChromaticAberration = (float)chromaticAberration;
                effect.EnableBlur = enableBlur ? 1 : 0;
                effect.BlurStrength = (float)blurStrength;
            }

            effect.Time = (float)totalSeconds;

            isFirst = false;
            this.controlMode = controlMode;
            this.temperature = temperature;
            this.humidity = humidity;
            this.strength = strength;
            this.scale = scale;
            this.flowSpeed = flowSpeed;
            this.boilSpeed = boilSpeed;
            this.angle = angle;
            this.chromaticAberration = chromaticAberration;
            this.enableBlur = enableBlur;
            this.blurStrength = blurStrength;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new HeatHazeCustomEffect(devices);
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
