using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.LightSource;
using YukkuriMovieMaker.Plugin.FileSource;
using Blend = Vortice.Direct2D1.Effects.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion
{
    using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;
    internal class ReflectionAndExtrusionEffectProcessor(IGraphicsDevicesAndContext devices, ReflectionAndExtrusionEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;

        IVideoEffectProcessor? heightmap;
        LuminanceToAlpha? luminanceToAlpha;
        InvertAlphaCustomEffect? invertAlpha;
        ID2D1Image? heightOutput;

        ILightingProcessor? highlight;
        GaussianBlur? highlightBlur;
        BevelAndFlatCompositeCustomEffect? bevelAndFlatComposite;
        Composite? highlightComposite;
        Blend? highlightBlendEffect;

        AlphaMask? alphaMask;
        LinearHdrCompositeCustomEffect? linearHdrComposite;
        BevelAmbientOcclusionCustomEffect? ambientOcclusion;

        /*
         heightmap -> luminanceToAlpha -> invertAlpha -> hightOutput

                                                  heightOutput -+
         hightOutput -> highlight -> highlightBlur -> bevelAndFlatComposite -> composite or blend -> alphaMask -> output
                            +-----------------------------------+                 input -+        input-+
         */

        protected bool isFirst = true;

        double blur;
        HeightmapParameterBase? heightmapParameter;
        LightingParameterBase? lightingParameter;
        Project.Blend highlightBlend;
        bool isInvertAlpha;
        bool isLinearHdrCompositeEnabled;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var heightmapParameter = item.Heightmap;
            var lightingParameter = item.Lighting;

            if (heightmap is null || this.heightmapParameter != heightmapParameter)
            {
                if (heightmap != null)
                    disposer.RemoveAndDispose(ref heightmap);
                heightmap = heightmapParameter.CreateHeightmapSource(devices);
                disposer.Collect(heightmap);
                heightmap.SetInput(input);
                luminanceToAlpha?.SetInput(0, heightmap.Output, true);
            }
            if(highlight is null || this.lightingParameter != lightingParameter)
            {
                if (highlight != null)
                    disposer.RemoveAndDispose(ref highlight);
                highlight = lightingParameter.CreateLightingProcessor(item, devices);
                disposer.Collect(highlight);
                highlight.SetInput(heightOutput);
                highlightBlur?.SetInput(0, highlight.Output, true);
                bevelAndFlatComposite?.SetInput(1, highlight.Output, true);
            }

            if (IsPassThroughEffect
                || heightmap is null || luminanceToAlpha is null || invertAlpha is null || heightOutput is null
                || highlight is null || highlightBlur is null || bevelAndFlatComposite is null || highlightComposite is null || highlightBlendEffect is null
                || alphaMask is null)
                return effectDescription.DrawDescription;

            effectDescription = effectDescription with { DrawDescription = heightmap.Update(effectDescription) };
            effectDescription = effectDescription with { DrawDescription = highlight.Update(effectDescription) };

            var blur = item.Blur.GetValue(frame, length, fps) / 3;
            var highlightBlend = highlight.Blend;
            var isInvertAlpha = item.IsInvert;
            var isLinearHdrCompositeEnabled = item.IsLinearHdrCompositeEnabled
                && linearHdrComposite is not null
                && IsLinearHdrBlendSupported(highlightBlend);
            var surfaceScale = lightingParameter.SurfaceScale.GetValue(frame, length, fps);
            var distance = item.OcclusionDistance.GetValue(frame, length, fps);
            var bias = item.OcclusionBias.GetValue(frame, length, fps);
            var softness = item.OcclusionSoftness.GetValue(frame, length, fps);
            var shadowStrength = item.SelfShadowStrength.GetValue(frame, length, fps) / 100;
            var aoStrength = item.AmbientOcclusionStrength.GetValue(frame, length, fps) / 100;

            if (isFirst || this.blur != blur)
                highlightBlur.StandardDeviation = (float)blur;

            if(isFirst || this.isInvertAlpha != isInvertAlpha)
                invertAlpha.Invert = isInvertAlpha ? 1 : 0;

            if (highlight is HighQualityLightingProcessor highQuality)
                highQuality.SetSelfShadowSettings(new((float)shadowStrength, (float)distance, (float)bias, (float)softness, item.OcclusionQuality));

            if (ambientOcclusion is not null)
            {
                ambientOcclusion.Strength = (float)aoStrength;
                ambientOcclusion.Distance = (float)distance;
                ambientOcclusion.Bias = (float)bias;
                ambientOcclusion.Softness = (float)softness;
                ambientOcclusion.SurfaceScale = (float)surfaceScale;
                ambientOcclusion.Quality = item.OcclusionQuality;
            }

            if (isFirst || this.highlightBlend != highlightBlend || this.isLinearHdrCompositeEnabled != isLinearHdrCompositeEnabled)
            {
                if (isLinearHdrCompositeEnabled)
                {
                    using var image = linearHdrComposite!.Output;
                    alphaMask.SetInput(0, image, true);
                    linearHdrComposite.BlendMode = highlightBlend;
                }
                else if (highlightBlend.IsCompositionEffect())
                {
                    using (var image = highlightComposite.Output)
                        alphaMask.SetInput(0, image, true);
                    highlightComposite.Mode = highlightBlend.ToD2DCompositionMode();
                }
                else
                {
                    using (var image = highlightBlendEffect.Output)
                        alphaMask.SetInput(0, image, true);
                    highlightBlendEffect.Mode = highlightBlend.ToD2DBlendMode();
                }
            }

            isFirst = false;
            this.heightmapParameter = heightmapParameter;
            this.lightingParameter = lightingParameter;
            this.blur = blur;
            this.highlightBlend = highlightBlend;
            this.isInvertAlpha = isInvertAlpha;
            this.isLinearHdrCompositeEnabled = isLinearHdrCompositeEnabled;

            return effectDescription.DrawDescription;
        }

        /// <summary>
        /// LinearHdrComposite.hlsl の BlendLinear が実装している値だけを許可する。
        /// シェーダー側の分岐を変更するときはこの一覧も同時に更新すること。
        /// </summary>
        internal static bool IsLinearHdrBlendSupported(Project.Blend blend)
            => blend is Project.Blend.Normal
            or Project.Blend.Add
            or Project.Blend.Subtract
            or Project.Blend.Multiply
            or Project.Blend.Screen
            or Project.Blend.Overlay
            or Project.Blend.Lighter
            or Project.Blend.Darker
            or Project.Blend.LinearBurn
            or Project.Blend.LinearLight
            or Project.Blend.Difference
            or Project.Blend.ColorBurn
            or Project.Blend.ColorDodge
            or Project.Blend.LinearDodge
            or Project.Blend.SoftLight
            or Project.Blend.HardLight
            or Project.Blend.VividLight
            or Project.Blend.PinLight
            or Project.Blend.HardMix
            or Project.Blend.Exclusion;

        protected override void ClearEffectChain()
        {
            heightmap?.SetInput(null);
            luminanceToAlpha?.SetInput(0, null, true);
            invertAlpha?.SetInput(0, null, true);

            highlight?.SetInput(null);
            highlightBlur?.SetInput(0, null, true);
            bevelAndFlatComposite?.SetInput(0, null, true);
            bevelAndFlatComposite?.SetInput(1, null, true);
            bevelAndFlatComposite?.SetInput(2, null, true);
            highlightComposite?.SetInput(0, null, true);
            highlightComposite?.SetInput(1, null, true);
            highlightBlendEffect?.SetInput(0, null, true);
            highlightBlendEffect?.SetInput(1, null, true);

            alphaMask?.SetInput(0, null, true);
            alphaMask?.SetInput(1, null, true);
            linearHdrComposite?.SetBaseInput(null);
            linearHdrComposite?.SetReflectionInput(null);
            ambientOcclusion?.SetInput(0, null, true);
            ambientOcclusion?.SetInput(1, null, true);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            //ハイトマップ
            luminanceToAlpha = new(devices.DeviceContext);
            disposer.Collect(luminanceToAlpha);

            invertAlpha = new(devices);
            disposer.Collect(invertAlpha);

            //ハイライト
            highlight = item.Lighting.CreateLightingProcessor(item, devices);
            disposer.Collect(highlight);

            highlightBlur = new(devices.DeviceContext);
            disposer.Collect(highlightBlur);

            bevelAndFlatComposite = new(devices);
            disposer.Collect(bevelAndFlatComposite);

            highlightComposite = new(devices.DeviceContext);
            disposer.Collect(highlightComposite);

            highlightBlendEffect = new(devices.DeviceContext);
            disposer.Collect(highlightBlendEffect);

            if (!invertAlpha.IsEnabled || !bevelAndFlatComposite.IsEnabled)
            {
                luminanceToAlpha?.Dispose();
                invertAlpha?.Dispose();
                highlight?.Dispose();
                highlightBlur?.Dispose();
                bevelAndFlatComposite?.Dispose();
                highlightComposite?.Dispose();
                highlightBlendEffect?.Dispose();

                luminanceToAlpha = null;
                invertAlpha = null;
                highlight = null;
                highlightBlur = null;
                bevelAndFlatComposite = null;
                highlightComposite = null;
                highlightBlendEffect = null;
                return null;
            }

            //後処理
            alphaMask = new(devices.DeviceContext);
            disposer.Collect(alphaMask);

            linearHdrComposite = new(devices);
            if (linearHdrComposite.IsEnabled)
                disposer.Collect(linearHdrComposite);
            else
            {
                linearHdrComposite.Dispose();
                linearHdrComposite = null;
            }

            ambientOcclusion = new(devices);
            if (ambientOcclusion.IsEnabled)
                disposer.Collect(ambientOcclusion);
            else
            {
                ambientOcclusion.Dispose();
                ambientOcclusion = null;
            }

            //接続（ハイトマップ）
            using(var image = luminanceToAlpha.Output)
                invertAlpha.SetInput(0, image, true);
            heightOutput = invertAlpha.Output;
            disposer.Collect(heightOutput);

            //接続（ハイライト）
            bevelAndFlatComposite.SetInput(0, heightOutput, true);
            using (var image = highlightBlur.Output)
                bevelAndFlatComposite.SetInput(2, image, true);
            using (var image = bevelAndFlatComposite.Output)
            {
                highlightComposite.SetInput(1, image, true);
                highlightBlendEffect.SetInput(1, image, true);
                linearHdrComposite?.SetReflectionInput(image);
            }

            ambientOcclusion?.SetInput(1, heightOutput, true);

            ID2D1Image output;
            if (ambientOcclusion is not null)
            {
                using var masked = alphaMask.Output;
                ambientOcclusion.SetInput(0, masked, true);
                output = ambientOcclusion.Output;
            }
            else
            {
                output = alphaMask.Output;
            }

            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            highlightComposite?.SetInput(0, input, true);
            highlightBlendEffect?.SetInput(0, input, true);
            alphaMask?.SetInput(1, input, true);
            linearHdrComposite?.SetBaseInput(input);
            heightmap?.SetInput(input);
        }
    }
}
