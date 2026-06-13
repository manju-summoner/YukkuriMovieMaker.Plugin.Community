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
using YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PartialOutline
{
    using AlphaMask = Vortice.Direct2D1.Effects.AlphaMask;
    using Blend = Vortice.Direct2D1.Effects.Blend;

    internal sealed class PartialOutlineEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly PartialOutlineEffect item;

        bool isFirst = true;
        double thickness, blur, opacity;
        int quality;
        double smoothness;
        double angle, position, width, softness;
        Project.Blend blend;
        bool isOutlineOnly;
        bool isAngular;
        RawRectF outlineBounds;
        RawRectF expandedInputBounds;
        RawRectF originalInputBounds;

        IBrushSource? brushSource;
        object? brushType;
        ID2D1CommandList? brushCommandList;

        Flood? floodEffect;
        Composite? expandedInputComposite;
        Crop? cropEffect;
        ID2D1Image? cropOutput;
        readonly List<InnerOutlineCustomEffect> dilationEffects = [];
        ID2D1Image? dilatedOutput;
        Composite? outerRingComposite;
        ID2D1Image? outerRingOutput;
        FeatherAlphaCustomEffect? featherAlphaEffect;
        PartialOutlineCustomEffect? partialMaskEffect;
        AlphaMask? alphaMaskEffect;
        GaussianBlur? blurEffect;
        Opacity? opacityEffect;
        ID2D1Image? opacityOutput;
        Composite? compositeEffect;
        Blend? blendEffect;
        AffineTransform2D? sink;

        public PartialOutlineEffectProcessor(IGraphicsDevicesAndContext devices, PartialOutlineEffect item) : base(devices)
        {
            this.devices = devices;
            this.item = item;
            brushType = item.Brush.Type;
            brushSource = item.Brush.CreateBrush(devices);
            if (brushSource != null)
                disposer.Collect(brushSource);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || brushSource is null
                || featherAlphaEffect is null
                || partialMaskEffect is null
                || alphaMaskEffect is null
                || blurEffect is null
                || opacityEffect is null
                || opacityOutput is null
                || compositeEffect is null
                || blendEffect is null
                || sink is null
                || cropEffect is null
                || outerRingComposite is null
                || outerRingOutput is null
                || input is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var thickness = Math.Round(item.Thickness.GetValue(frame, length, fps), 1);
            var blur = item.Blur.GetValue(frame, length, fps);
            var opacity = item.Opacity.GetValue(frame, length, fps);
            var quality = (int)item.Quality.GetValue(frame, length, fps);
            var smoothness = item.Smoothness.GetValue(frame, length, fps) / 100.0;
            var angle = item.Angle.GetValue(frame, length, fps);
            var position = item.Position.GetValue(frame, length, fps);
            var width = item.Width.GetValue(frame, length, fps);
            var softness = item.Softness.GetValue(frame, length, fps);
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
            var expandPadding = Math.Max(thickness, blur * 3);
            var expandedBounds = new RawRectF(
                inputBounds.Left - (float)expandPadding,
                inputBounds.Top - (float)expandPadding,
                inputBounds.Right + (float)expandPadding,
                inputBounds.Bottom + (float)expandPadding);

            if (isFirst || !this.expandedInputBounds.Equals(expandedBounds))
                cropEffect.Rectangle = new Vector4(expandedBounds.Left, expandedBounds.Top, expandedBounds.Right, expandedBounds.Bottom);

            if (isFirst
                || this.angle != angle
                || this.position != position
                || this.width != width
                || this.softness != softness
                || !this.originalInputBounds.Equals(inputBounds))
            {
                float halfW = (inputBounds.Right - inputBounds.Left) * 0.5f;
                float halfH = (inputBounds.Bottom - inputBounds.Top) * 0.5f;
                float angleRad = (float)(angle * Math.PI / 180.0);
                float halfExtent = (float)(Math.Abs(Math.Cos(angleRad)) * halfW + Math.Abs(Math.Sin(angleRad)) * halfH);

                partialMaskEffect.Angle = angleRad;
                partialMaskEffect.BandCenter = (float)(position / 100.0) * halfExtent;
                partialMaskEffect.HalfBandWidth = (float)(width / 200.0) * halfExtent;
                partialMaskEffect.Softness = (float)(softness / 100.0);
                partialMaskEffect.CenterX = inputBounds.Left + halfW;
                partialMaskEffect.CenterY = inputBounds.Top + halfH;
            }

            var currentOutlineBounds = dc.GetImageLocalBounds(outerRingOutput);
            var newBrushType = item.Brush.Type;
            if (!Equals(this.brushType, newBrushType))
            {
                disposer.RemoveAndDispose(ref brushSource);
                brushSource = item.Brush.CreateBrush(devices);
                if (brushSource != null)
                    disposer.Collect(brushSource);
                else
                    return effectDescription.DrawDescription;
            }
            var isBrushChanged = brushSource.Update(effectDescription) || !Equals(this.brushType, newBrushType);
            if (isFirst || isBrushChanged || !this.outlineBounds.Equals(currentOutlineBounds))
            {
                if (brushCommandList != null)
                    disposer.RemoveAndDispose(ref brushCommandList);
                brushCommandList = devices.DeviceContext.CreateCommandList();
                disposer.Collect(brushCommandList);

                dc.Target = brushCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.FillRectangle(currentOutlineBounds, brushSource.Brush);
                dc.EndDraw();
                dc.Target = null;
                brushCommandList.Close();

                alphaMaskEffect.SetInput(0, brushCommandList, true);
            }

            if (isFirst || this.isOutlineOnly != isOutlineOnly || this.blend != blend)
            {
                if (isOutlineOnly)
                {
                    sink.SetInput(0, opacityOutput, true);
                }
                else if (blend.IsCompositionEffect())
                {
                    compositeEffect.Mode = blend.ToD2DCompositionMode();
                    using var output = compositeEffect.Output;
                    sink.SetInput(0, output, true);
                }
                else
                {
                    blendEffect.Mode = blend.ToD2DBlendMode();
                    using var output = blendEffect.Output;
                    sink.SetInput(0, output, true);
                }
            }

            isFirst = false;
            this.thickness = thickness;
            this.blur = blur;
            this.opacity = opacity;
            this.quality = quality;
            this.smoothness = smoothness;
            this.angle = angle;
            this.position = position;
            this.width = width;
            this.softness = softness;
            this.blend = blend;
            this.isOutlineOnly = isOutlineOnly;
            this.isAngular = isAngular;
            this.brushType = newBrushType;
            this.outlineBounds = currentOutlineBounds;
            this.expandedInputBounds = expandedBounds;
            this.originalInputBounds = inputBounds;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            floodEffect = new Flood(devices.DeviceContext) { Color = new Vector4(0f, 0f, 0f, 0f) };
            disposer.Collect(floodEffect);

            expandedInputComposite = new Composite(devices.DeviceContext);
            disposer.Collect(expandedInputComposite);

            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);

            using (var floodOut = floodEffect.Output)
                expandedInputComposite.SetInput(0, floodOut, true);
            using (var compositeOut = expandedInputComposite.Output)
                cropEffect.SetInput(0, compositeOut, true);

            cropOutput = cropEffect.Output;
            disposer.Collect(cropOutput);

            EnsureDilationCapacity(devices, 1);

            if (dilationEffects.Count == 0)
                return null;

            outerRingComposite = new Composite(devices.DeviceContext);
            outerRingComposite.Mode = CompositeMode.SourceOut;
            disposer.Collect(outerRingComposite);

            if (dilatedOutput != null)
                outerRingComposite.SetInput(1, dilatedOutput, true);

            outerRingOutput = outerRingComposite.Output;
            disposer.Collect(outerRingOutput);

            featherAlphaEffect = new FeatherAlphaCustomEffect(devices);
            if (!featherAlphaEffect.IsEnabled)
            {
                featherAlphaEffect.Dispose();
                featherAlphaEffect = null;
                return null;
            }
            disposer.Collect(featherAlphaEffect);
            featherAlphaEffect.SetInput(0, outerRingOutput, true);

            partialMaskEffect = new PartialOutlineCustomEffect(devices);
            if (!partialMaskEffect.IsEnabled)
            {
                partialMaskEffect.Dispose();
                partialMaskEffect = null;
                return null;
            }
            disposer.Collect(partialMaskEffect);
            using (var featheredOut = featherAlphaEffect.Output)
                partialMaskEffect.SetInput(0, featheredOut, true);

            alphaMaskEffect = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);
            using (var maskedOut = partialMaskEffect.Output)
                alphaMaskEffect.SetInput(1, maskedOut, true);

            blurEffect = new GaussianBlur(devices.DeviceContext);
            disposer.Collect(blurEffect);
            using (var alphaMaskedOut = alphaMaskEffect.Output)
                blurEffect.SetInput(0, alphaMaskedOut, true);

            opacityEffect = new Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);
            using (var blurredOut = blurEffect.Output)
                opacityEffect.SetInput(0, blurredOut, true);

            opacityOutput = opacityEffect.Output;
            disposer.Collect(opacityOutput);

            compositeEffect = new Composite(devices.DeviceContext);
            disposer.Collect(compositeEffect);
            compositeEffect.SetInput(1, opacityOutput, true);

            blendEffect = new Blend(devices.DeviceContext);
            disposer.Collect(blendEffect);
            blendEffect.SetInput(1, opacityOutput, true);

            sink = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(sink);

            var result = sink.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            if (dilationEffects.Count > 0 && cropOutput is not null)
                dilationEffects[0].SetInput(0, cropOutput, true);
            expandedInputComposite?.SetInput(1, input, true);
            outerRingComposite?.SetInput(0, input, true);
            compositeEffect?.SetInput(0, input, true);
            blendEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            for (int i = 0; i < dilationEffects.Count; i++)
                dilationEffects[i].SetInput(0, null, true);
            featherAlphaEffect?.SetInput(0, null, true);
            partialMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(1, null, true);
            blurEffect?.SetInput(0, null, true);
            opacityEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(1, null, true);
            blendEffect?.SetInput(0, null, true);
            blendEffect?.SetInput(1, null, true);
            expandedInputComposite?.SetInput(0, null, true);
            expandedInputComposite?.SetInput(1, null, true);
            cropEffect?.SetInput(0, null, true);
            outerRingComposite?.SetInput(0, null, true);
            outerRingComposite?.SetInput(1, null, true);
            sink?.SetInput(0, null, true);
        }

        void ApplyDilationPasses(double thickness, int samples, bool isAngular)
        {
            var steps = DecomposeThicknessToSteps(thickness);
            var requiredCount = Math.Max(1, steps.Count);

            if (dilationEffects.Count != requiredCount)
                EnsureDilationCapacity(devices, requiredCount);

            for (int i = 0; i < dilationEffects.Count; i++)
            {
                var step = steps[i];
                dilationEffects[i].Samples = Math.Min(samples, (int)Math.Pow(step * 2, 2) + 1);
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

            if (dilationEffects.Count == 0 || cropOutput is null)
                return;

            dilationEffects[0].SetInput(0, cropOutput, true);
            for (int i = 1; i < dilationEffects.Count; i++)
            {
                using var prev = dilationEffects[i - 1].Output;
                dilationEffects[i].SetInput(0, prev, true);
            }

            if (dilatedOutput != null)
                disposer.RemoveAndDispose(ref dilatedOutput);

            dilatedOutput = dilationEffects[^1].Output;
            disposer.Collect(dilatedOutput);

            outerRingComposite?.SetInput(1, dilatedOutput, true);
        }

        static List<float> DecomposeThicknessToSteps(double thickness)
        {
            var steps = new List<float>();
            for (int i = 0; ; i++)
            {
                var step = Math.Min(thickness, Math.Pow(2, i));
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
