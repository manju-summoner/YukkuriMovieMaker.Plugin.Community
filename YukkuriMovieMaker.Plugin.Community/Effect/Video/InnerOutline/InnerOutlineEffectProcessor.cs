using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;

    public class InnerOutlineEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly InnerOutlineEffect item;

        bool isFirst = true;
        bool isInputChanged = false;
        double thickness, blur, opacity;
        int quality;
        double smoothness;
        Project.Blend blend;
        bool isOutlineOnly;
        bool isAngular;
        RawRectF bounds;
        RawRectF expandedInputBounds;
        double expandPadding;

        readonly IBrushSource brushSource;
        readonly ID2D1SolidColorBrush transparentBrush;
        ID2D1CommandList? brushCommandList;
        ID2D1CommandList? expandedInputCommandList;

        InvertAlphaCustomEffect? invertAlphaEffect;
        ID2D1Image? invertAlphaOutput;
        readonly List<InnerOutlineCustomEffect> dilationEffects = [];
        ID2D1Image? outlineOutput;
        FeatherAlphaCustomEffect? featherAlphaEffect;
        AlphaMask? alphaMaskEffect;
        GaussianBlur? blurEffect;
        Opacity? opacityEffect;
        AlphaMask? alphaMaskEffect2;
        ID2D1Image? opacityOutput;
        ID2D1CommandList? commandList;

        public InnerOutlineEffectProcessor(IGraphicsDevicesAndContext devices, InnerOutlineEffect item) : base(devices)
        {
            this.devices = devices;
            this.item = item;
            brushSource = item.Brush.CreateBrush(devices);
            disposer.Collect(brushSource);
            transparentBrush = devices.DeviceContext.CreateSolidColorBrush(new Vortice.Mathematics.Color4(0, 0, 0, 0));
            disposer.Collect(transparentBrush);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || invertAlphaEffect is null
                || outlineOutput is null
                || featherAlphaEffect is null
                || alphaMaskEffect is null
                || blurEffect is null
                || opacityEffect is null
                || alphaMaskEffect2 is null
                || opacityOutput is null
                || input is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var thickness = System.Math.Round(item.Thickness.GetValue(frame, length, fps), 1);
            var blur = item.Blur.GetValue(frame, length, fps);
            var opacity = item.Opacity.GetValue(frame, length, fps);
            var quality = (int)item.Quality.GetValue(frame, length, fps);
            var smoothness = item.Smoothness.GetValue(frame, length, fps) / 100;
            var blend = item.Blend;
            var isOutlineOnly = item.IsOutlineOnly;
            var isAngular = item.IsAngular;

            if (isFirst || this.thickness != thickness || this.quality != quality || this.isAngular != isAngular)
                ApplyDilationPasses(thickness, quality, isAngular);
            if (isFirst || this.blur != blur)
                blurEffect.StandardDeviation = (float)blur;
            if (isFirst || this.opacity != opacity)
                opacityEffect.Value = (float)opacity / 100f;
            if (isFirst || this.smoothness != smoothness)
                featherAlphaEffect.Smoothness = (float)smoothness;

            var dc = devices.DeviceContext;
            var inputBounds = dc.GetImageLocalBounds(input);
            var expandPadding = System.Math.Max(thickness, blur * 3);
            var expandedBounds = new RawRectF(
                inputBounds.Left - (float)expandPadding,
                inputBounds.Top - (float)expandPadding,
                inputBounds.Right + (float)expandPadding,
                inputBounds.Bottom + (float)expandPadding);

            if (isFirst || isInputChanged || !this.expandedInputBounds.Equals(expandedBounds) || this.expandPadding != expandPadding)
            {
                if (expandedInputCommandList != null)
                    disposer.RemoveAndDispose(ref expandedInputCommandList);
                expandedInputCommandList = dc.CreateCommandList();
                disposer.Collect(expandedInputCommandList);

                dc.Target = expandedInputCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.DrawRectangle(expandedBounds, transparentBrush);
                dc.DrawImage(input);
                dc.EndDraw();
                dc.Target = null;
                expandedInputCommandList.Close();

                invertAlphaEffect.SetInput(0, expandedInputCommandList, true);
            }

            var bounds = dc.GetImageLocalBounds(outlineOutput);
            var isBrushChanged = brushSource.Update(effectDescription);
            if (isFirst || isBrushChanged || !this.bounds.Equals(bounds))
            {
                if (brushCommandList != null)
                    disposer.RemoveAndDispose(ref brushCommandList);
                brushCommandList = devices.DeviceContext.CreateCommandList();
                disposer.Collect(brushCommandList);

                dc.Target = brushCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.FillRectangle(bounds, brushSource.Brush);
                dc.EndDraw();
                dc.Target = null;
                brushCommandList.Close();

                alphaMaskEffect.SetInput(0, brushCommandList, true);
            }

            if (commandList != null)
                disposer.RemoveAndDispose(ref commandList);
            commandList = devices.DeviceContext.CreateCommandList();
            disposer.Collect(commandList);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (!isOutlineOnly)
                dc.DrawImage(input);
            if (blend.IsCompositionEffect())
                dc.DrawImage(opacityOutput, compositeMode: blend.ToD2DCompositionMode());
            else
                dc.BlendImage(opacityOutput, blend.ToD2DBlendMode(), Vector2.Zero, null, InterpolationMode.HighQualityCubic);
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();
            alphaMaskEffect2.SetInput(0, commandList, true);

            isFirst = false;
            isInputChanged = false;
            this.thickness = thickness;
            this.blur = blur;
            this.opacity = opacity;
            this.quality = quality;
            this.smoothness = smoothness;
            this.blend = blend;
            this.isOutlineOnly = isOutlineOnly;
            this.isAngular = isAngular;
            this.bounds = bounds;
            this.expandedInputBounds = expandedBounds;
            this.expandPadding = expandPadding;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            invertAlphaEffect = new InvertAlphaCustomEffect(devices)
            {
                Invert = 1,
            };
            if (!invertAlphaEffect.IsEnabled)
            {
                invertAlphaEffect.Dispose();
                invertAlphaEffect = null;
                return null;
            }
            disposer.Collect(invertAlphaEffect);

            invertAlphaOutput = invertAlphaEffect.Output;
            disposer.Collect(invertAlphaOutput);

            EnsureDilationCapacity(devices, 1);

            featherAlphaEffect = new FeatherAlphaCustomEffect(devices);
            if (!featherAlphaEffect.IsEnabled)
            {
                featherAlphaEffect.Dispose();
                featherAlphaEffect = null;
                return null;
            }
            disposer.Collect(featherAlphaEffect);

            alphaMaskEffect = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            blurEffect = new GaussianBlur(devices.DeviceContext);
            disposer.Collect(blurEffect);

            opacityEffect = new Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            alphaMaskEffect2 = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect2);

            featherAlphaEffect.SetInput(0, outlineOutput, true);
            using (var output = featherAlphaEffect.Output)
                alphaMaskEffect.SetInput(1, output, true);
            using (var output = alphaMaskEffect.Output)
                blurEffect.SetInput(0, output, true);
            using (var output = blurEffect.Output)
                opacityEffect.SetInput(0, output, true);

            opacityOutput = opacityEffect.Output;
            disposer.Collect(opacityOutput);

            var result = alphaMaskEffect2.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            isInputChanged = true;
            if (dilationEffects.Count > 0 && invertAlphaOutput is not null)
                dilationEffects[0].SetInput(0, invertAlphaOutput, true);
            alphaMaskEffect2?.SetInput(1, input, true);
        }

        protected override void ClearEffectChain()
        {
            invertAlphaEffect?.SetInput(0, null, true);
            for (int i = 0; i < dilationEffects.Count; i++)
                dilationEffects[i].SetInput(0, null, true);
            featherAlphaEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(1, null, true);
            blurEffect?.SetInput(0, null, true);
            opacityEffect?.SetInput(0, null, true);
            alphaMaskEffect2?.SetInput(0, null, true);
            alphaMaskEffect2?.SetInput(1, null, true);
        }

        void ApplyDilationPasses(double thickness, int samples, bool isAngular)
        {
            var steps = DecomposeThicknessToSteps(thickness);
            var requiredCount = System.Math.Max(1, steps.Count);

            if (dilationEffects.Count != requiredCount)
                EnsureDilationCapacity(devices, requiredCount);

            for (int i = 0; i < dilationEffects.Count; i++)
            {
                var step = steps[i];
                dilationEffects[i].Samples = System.Math.Min(samples, (int)System.Math.Pow(step * 2, 2) + 1);
                dilationEffects[i].StepPx = step;
                dilationEffects[i].IsAngular = isAngular;
            }
        }

        void EnsureDilationCapacity(IGraphicsDevicesAndContext devices, int count)
        {
            while (dilationEffects.Count < count)
            {
                var effect = new InnerOutlineCustomEffect(devices);
                if (!effect.IsEnabled)
                {
                    effect.Dispose();
                    break;
                }
                dilationEffects.Add(effect);
                disposer.Collect(effect);
            }
            while (dilationEffects.Count > count)
            {
                var effect = dilationEffects[^1];
                effect.SetInput(0, null, true);
                disposer.RemoveAndDispose(ref effect);
                dilationEffects.RemoveAt(dilationEffects.Count - 1);
            }

            for (int i = 0; i < dilationEffects.Count; i++)
                dilationEffects[i].Cached = i == dilationEffects.Count - 1;

            for (int i = 1; i < dilationEffects.Count; i++)
                dilationEffects[i].SetInput(0, null, true);

            if (dilationEffects.Count == 0 || invertAlphaOutput is null)
                return;

            dilationEffects[0].SetInput(0, invertAlphaOutput, true);
            for (int i = 1; i < dilationEffects.Count; i++)
            {
                using var prev = dilationEffects[i - 1].Output;
                dilationEffects[i].SetInput(0, prev, true);
            }

            if (outlineOutput != null)
                disposer.RemoveAndDispose(ref outlineOutput);

            outlineOutput = dilationEffects[^1].Output;
            disposer.Collect(outlineOutput);

            featherAlphaEffect?.SetInput(0, outlineOutput, true);
        }

        static List<float> DecomposeThicknessToSteps(double thickness)
        {
            var steps = new List<float>();
            for (int i = 0; ; i++)
            {
                var step = System.Math.Min(thickness, System.Math.Pow(2, i));
                if (step <= 0)
                    break;
                steps.Add((float)step);
                thickness -= step;
            }
            if (steps.Count is 0)
                steps.Add(0f);

            return [.. steps.OrderByDescending(x => x)];
        }
    }
}
