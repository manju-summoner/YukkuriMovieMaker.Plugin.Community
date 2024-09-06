using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using D2D = Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorCorrection
{
    public class ColorCorrectionEffectProcessor(IGraphicsDevicesAndContext devices, ColorCorrectionEffect item) : VideoEffectProcessorBase(devices)
    {
        bool isFirstUpdate = true;
        double lightness;
        double contrast;

        double hue;
        double brightness;
        double saturation;

        ColorCorrectionCustomEffect? colorCorrectionEffect;

        protected override D2D.ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            colorCorrectionEffect = new ColorCorrectionCustomEffect(devices);
            if (!colorCorrectionEffect.IsEnabled)
            {
                colorCorrectionEffect.Dispose();
                colorCorrectionEffect = null;
                return null;
            }
            disposer.Collect(colorCorrectionEffect);

            var output = colorCorrectionEffect.Output;
            disposer.Collect(output);
            return output;
        }
        protected override void setInput(D2D.ID2D1Image? input)
        {
            colorCorrectionEffect?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain() => SetInput(null);

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            //ShaderModel5.0非対応環境用
            if (IsPassThroughEffect || colorCorrectionEffect is null) return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var currentLightness = item.Lightness.GetValue(frame, length, fps);
            var currentContrast = item.Contrast.GetValue(frame, length, fps);
            var currentHue = item.HueRotation.GetValue(frame, length, fps) % 360;
            if (currentHue < 0) currentHue += 360;
            var currentBrightness = item.Brightness.GetValue(frame, length, fps);
            var currentSaturation = item.Saturation.GetValue(frame, length, fps);

            if (isFirstUpdate || lightness != currentLightness)
                colorCorrectionEffect.Lightness = (float)(currentLightness / 100);
            if (isFirstUpdate || contrast != currentContrast)
                colorCorrectionEffect.Contrast = (float)(currentContrast / 100);

            if (isFirstUpdate || hue != currentHue)
                colorCorrectionEffect.Hue = (float)(currentHue / 360);
            if (isFirstUpdate || brightness != currentBrightness)
                colorCorrectionEffect.Brightness = (float)(currentBrightness / 100);
            if (isFirstUpdate || saturation != currentSaturation)
                colorCorrectionEffect.Saturation = (float)(currentSaturation / 100);


            isFirstUpdate = false;
            lightness = currentLightness;
            contrast = currentContrast;
            hue = currentHue;
            brightness = currentBrightness;
            saturation = currentSaturation;

            return effectDescription.DrawDescription;
        }
    }

}
