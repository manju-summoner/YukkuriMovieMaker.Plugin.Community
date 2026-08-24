using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    internal sealed class AnisotropicKuwaharaEffectProcessor(IGraphicsDevicesAndContext devices, AnisotropicKuwaharaEffect item) : VideoEffectProcessorBase(devices)
    {
        // パス1: 構造テンソル -> パス2: 平滑化 -> パス3: 本体(入力0=元画像, 入力1=平滑テンソル)
        AnisotropicKuwaharaTensorCustomEffect? tensorEffect;
        AnisotropicKuwaharaTensorBlurCustomEffect? tensorBlurXEffect;
        AnisotropicKuwaharaTensorBlurCustomEffect? tensorBlurYEffect;
        AnisotropicKuwaharaCustomEffect? kuwaharaEffect;

        bool isFirst = true;
        double radius, sharpness, anisotropy;
        AnisotropicKuwaharaQuality quality;

        static int QualityToMaxN(AnisotropicKuwaharaQuality q) => q switch
        {
            AnisotropicKuwaharaQuality.Low => 6,
            AnisotropicKuwaharaQuality.Medium => 10,
            AnisotropicKuwaharaQuality.High => 15,
            AnisotropicKuwaharaQuality.Ultra => 22,
            _ => 15,
        };

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || kuwaharaEffect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var radius = item.Radius.GetValue(frame, length, fps);
            var sharpness = item.Sharpness.GetValue(frame, length, fps);
            var anisotropy = item.Anisotropy.GetValue(frame, length, fps);
            var quality = item.Quality;

            if (isFirst
                || this.radius != radius
                || this.sharpness != sharpness
                || this.anisotropy != anisotropy
                || this.quality != quality)
            {
                kuwaharaEffect.Radius = (float)radius;
                kuwaharaEffect.Sharpness = (float)sharpness;
                kuwaharaEffect.Anisotropy = (float)(anisotropy / 100.0);
                kuwaharaEffect.MaxN = QualityToMaxN(quality);
            }

            isFirst = false;
            this.radius = radius;
            this.sharpness = sharpness;
            this.anisotropy = anisotropy;
            this.quality = quality;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            tensorEffect = new AnisotropicKuwaharaTensorCustomEffect(devices);
            tensorBlurXEffect = new AnisotropicKuwaharaTensorBlurCustomEffect(devices);
            tensorBlurYEffect = new AnisotropicKuwaharaTensorBlurCustomEffect(devices);
            kuwaharaEffect = new AnisotropicKuwaharaCustomEffect(devices);

            if (!tensorEffect.IsEnabled || !tensorBlurXEffect.IsEnabled || !tensorBlurYEffect.IsEnabled || !kuwaharaEffect.IsEnabled)
            {
                tensorEffect.Dispose();
                tensorBlurXEffect.Dispose();
                tensorBlurYEffect.Dispose();
                kuwaharaEffect.Dispose();
                tensorEffect = null;
                tensorBlurXEffect = null;
                tensorBlurYEffect = null;
                kuwaharaEffect = null;
                return null;
            }

            disposer.Collect(tensorEffect);
            disposer.Collect(tensorBlurXEffect);
            disposer.Collect(tensorBlurYEffect);
            disposer.Collect(kuwaharaEffect);

            tensorBlurXEffect.IsVertical = false;
            tensorBlurYEffect.IsVertical = true;

            // テンソル -> 平滑化 -> 本体の入力1
            using (var tensorOutput = tensorEffect.Output)
                tensorBlurXEffect.SetInput(0, tensorOutput, true);
            using (var blurXOutput = tensorBlurXEffect.Output)
                tensorBlurYEffect.SetInput(0, blurXOutput, true);
            using (var blurYOutput = tensorBlurYEffect.Output)
                kuwaharaEffect.SetInput(1, blurYOutput, true);

            var output = kuwaharaEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            // 元画像はパス1(テンソル)と本体の入力0の両方へ供給
            tensorEffect?.SetInput(0, input, true);
            kuwaharaEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            tensorEffect?.SetInput(0, null, true);
            tensorBlurXEffect?.SetInput(0, null, true);
            tensorBlurYEffect?.SetInput(0, null, true);
            kuwaharaEffect?.SetInput(0, null, true);
            kuwaharaEffect?.SetInput(1, null, true);
        }
    }
}
