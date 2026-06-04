using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Player;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype
{
    public class FillSametypeProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly FillSametypeEffect item;

        AffineTransform2D? outputEffect;
        ID2D1CommandList? brushCommandList;
        ID2D1CommandList? blendedCommandList;
        ID2D1CommandList? luminanceCommandList;

        IBrushSource? brushSource;
        readonly ID2D1SolidColorBrush transparentBrush;

        FillSametypeCustomEffect? colorMatchEffect;
        ID2D1Image? colorMatchOutput;
        GaussianBlur? blurEffect;
        Vortice.Direct2D1.Effects.AlphaMask? alphaMaskEffect;
        Vortice.Direct2D1.Effects.AlphaMask? luminanceMaskEffect;
        Opacity? opacityEffect;
        AffineTransform2D? finalMaskTransform;
        ID2D1Image? finalMaskTransformOutput;

        ID2D1Bitmap1? finalMaskBitmap;
        int finalMaskWidth, finalMaskHeight;

        ID2D1Bitmap1? candidateBitmap;
        ID2D1Bitmap1? candidateStagingBitmap;
        int candidateWidth, candidateHeight;

        ID2D1Bitmap1? seedBitmap;
        ID2D1Bitmap1? seedStagingBitmap;
        readonly byte[] seedPixel = new byte[4];

        readonly FillSametypePipeline pipeline = new();
        int[]? foregroundBuffer;
        int[]? maskBuffer;
        int bufferPixelCount;

        bool isFirst = true;
        ID2D1Image? currentInput;
        Type? brushType;
        RawRectF lastBounds;
        double opacity, blur, tolerance, shapeThreshold, posX, posY;
        bool isInverted;
        Vector4 lastMatchColor;
        double lastForegroundTolerance;
        int lastComponentCount;

        public FillSametypeProcessor(IGraphicsDevicesAndContext devices, FillSametypeEffect item)
            : base(devices)
        {
            this.devices = devices;
            this.item = item;

            transparentBrush = devices.DeviceContext.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0f));
            disposer.Collect(transparentBrush);
            disposer.Collect(pipeline);

            colorMatchEffect = new FillSametypeCustomEffect(devices);
            if (!colorMatchEffect.IsEnabled)
            {
                colorMatchEffect.Dispose();
                colorMatchEffect = null;
            }
            else
            {
                colorMatchOutput = colorMatchEffect.Output;
                disposer.Collect(colorMatchEffect);
                disposer.Collect(colorMatchOutput);
            }

            blurEffect = new GaussianBlur(devices.DeviceContext)
            {
                BorderMode = BorderMode.Hard,
                Optimization = GaussianBlurOptimization.Balanced,
            };
            disposer.Collect(blurEffect);

            alphaMaskEffect = new Vortice.Direct2D1.Effects.AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            luminanceMaskEffect = new Vortice.Direct2D1.Effects.AlphaMask(devices.DeviceContext);
            disposer.Collect(luminanceMaskEffect);

            opacityEffect = new Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            finalMaskTransform = new AffineTransform2D(devices.DeviceContext)
            {
                BorderMode = BorderMode.Hard,
            };
            disposer.Collect(finalMaskTransform);

            finalMaskTransformOutput = finalMaskTransform.Output;
            disposer.Collect(finalMaskTransformOutput);

            using var alphaMaskOutput = alphaMaskEffect.Output;
            opacityEffect.SetInput(0, alphaMaskOutput, true);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            outputEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(outputEffect);

            var output = outputEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
        }

        protected override void ClearEffectChain()
        {
            colorMatchEffect?.SetInput(0, null, true);
            outputEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(0, null, true);
            alphaMaskEffect?.SetInput(1, null, true);
            luminanceMaskEffect?.SetInput(0, null, true);
            luminanceMaskEffect?.SetInput(1, null, true);
            blurEffect?.SetInput(0, null, true);
            finalMaskTransform?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || outputEffect is null || input is null)
                return effectDescription.DrawDescription;

            if (colorMatchEffect is null || colorMatchOutput is null || alphaMaskEffect is null || opacityEffect is null)
            {
                outputEffect.SetInput(0, input, true);
                return effectDescription.DrawDescription;
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(input);
            int width = (int)Math.Ceiling(bounds.Right - bounds.Left);
            int height = (int)Math.Ceiling(bounds.Bottom - bounds.Top);

            if (width <= 0 || height <= 0)
                return effectDescription.DrawDescription;

            var currentOpacity = item.Opacity.GetValue(frame, length, fps);
            var currentBlur = Math.Max(0, item.Blur.GetValue(frame, length, fps));
            var currentBlendMode = item.BlendMode;
            var currentIsInverted = item.IsInverted;
            var currentIsBrushOnly = item.IsBrushOnly;
            var currentPreserveLuminance = item.PreserveLuminance;
            var currentTolerance = item.Tolerance.GetValue(frame, length, fps);
            var currentShapeThreshold = item.ShapeThreshold.GetValue(frame, length, fps);
            var currentX = item.X.GetValue(frame, length, fps);
            var currentY = item.Y.GetValue(frame, length, fps);
            var currentBrushType = item.Brush.Type;

            bool brushUpdated = false;
            if (isFirst || brushType != currentBrushType)
            {
                disposer.RemoveAndDispose(ref brushSource);
                brushSource = item.Brush.CreateBrush(devices);
                disposer.Collect(brushSource);
                brushUpdated = true;
            }
            brushUpdated |= brushSource?.Update(effectDescription) ?? false;

            bool needsMaskUpdate = isFirst
                || currentInput != input
                || !lastBounds.Equals(bounds)
                || tolerance != currentTolerance
                || shapeThreshold != currentShapeThreshold
                || isInverted != currentIsInverted
                || posX != currentX
                || posY != currentY;

            ID2D1Image? maskSource;
            if (needsMaskUpdate)
            {
                maskSource = PrepareSametypeMask(dc, bounds, width, height, currentX, currentY, currentTolerance, currentShapeThreshold, currentIsInverted);
            }
            else
            {
                maskSource = finalMaskTransformOutput;
            }

            if (maskSource is null)
            {
                outputEffect.SetInput(0, input, true);
                return effectDescription.DrawDescription;
            }

            if (isFirst || currentBlur != blur)
                UpdateAlphaMask(maskSource, currentBlur);
            if (isFirst || currentOpacity != opacity)
                opacityEffect.Value = (float)(Math.Clamp(currentOpacity, 0, 100) / 100.0);

            disposer.RemoveAndDispose(ref brushCommandList);
            brushCommandList = dc.CreateCommandList();
            disposer.Collect(brushCommandList);

            dc.Target = brushCommandList;
            dc.BeginDraw();
            dc.Clear(null);
            dc.FillRectangle(bounds, brushSource?.Brush ?? transparentBrush);
            dc.EndDraw();
            dc.Target = null;
            brushCommandList.Close();
            alphaMaskEffect.SetInput(0, brushCommandList, true);

            disposer.RemoveAndDispose(ref blendedCommandList);
            blendedCommandList = dc.CreateCommandList();
            disposer.Collect(blendedCommandList);

            dc.Target = blendedCommandList;
            dc.BeginDraw();
            dc.Clear(null);
            if (!currentIsBrushOnly)
                dc.DrawImage(input, InterpolationMode.MultiSampleLinear, CompositeMode.SourceOver);

            using (var opacityOutput = opacityEffect.Output)
            {
                if (currentBlendMode.IsCompositionEffect())
                    dc.DrawImage(opacityOutput, InterpolationMode.MultiSampleLinear, currentBlendMode.ToD2DCompositionMode());
                else
                    dc.BlendImage(opacityOutput, currentBlendMode.ToD2DBlendMode(), null, null, effectDescription.DrawDescription.ZoomInterpolationMode);
            }
            dc.EndDraw();
            dc.Target = null;
            blendedCommandList.Close();

            if (currentPreserveLuminance)
            {
                disposer.RemoveAndDispose(ref luminanceCommandList);
                luminanceCommandList = dc.CreateCommandList();
                disposer.Collect(luminanceCommandList);

                dc.Target = luminanceCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.DrawImage(blendedCommandList, InterpolationMode.MultiSampleLinear, CompositeMode.SourceOver);

                if (currentIsBrushOnly && luminanceMaskEffect is not null)
                {
                    luminanceMaskEffect.SetInput(0, input, true);
                    luminanceMaskEffect.SetInput(1, blendedCommandList, true);
                    using var luminanceSource = luminanceMaskEffect.Output;
                    dc.BlendImage(luminanceSource, Vortice.Direct2D1.BlendMode.Luminosity, null, null, effectDescription.DrawDescription.ZoomInterpolationMode);
                }
                else
                {
                    dc.BlendImage(input, Vortice.Direct2D1.BlendMode.Luminosity, null, null, effectDescription.DrawDescription.ZoomInterpolationMode);
                }
                dc.EndDraw();
                dc.Target = null;
                luminanceCommandList.Close();

                outputEffect.SetInput(0, luminanceCommandList, true);
            }
            else
            {
                outputEffect.SetInput(0, blendedCommandList, true);
            }

            isFirst = false;
            currentInput = input;
            lastBounds = bounds;
            opacity = currentOpacity;
            blur = currentBlur;
            isInverted = currentIsInverted;
            tolerance = currentTolerance;
            shapeThreshold = currentShapeThreshold;
            posX = currentX;
            posY = currentY;
            brushType = currentBrushType;

            var controller = new VideoEffectController(
                item,
                [
                    new ControllerPoint(
                        new Vector3((float)currentX, (float)currentY, 0f),
                        e =>
                        {
                            item.X.AddToEachValues(e.Delta.X);
                            item.Y.AddToEachValues(e.Delta.Y);
                        })
                ]);

            return effectDescription.DrawDescription with
            {
                Controllers =
                [
                    ..effectDescription.DrawDescription.Controllers,
                    controller
                ]
            };
        }

        ID2D1Image? PrepareSametypeMask(
            ID2D1DeviceContext dc,
            RawRectF bounds,
            int width,
            int height,
            double x,
            double y,
            double toleranceRaw,
            double shapeThresholdRaw,
            bool invert)
        {
            int pixelCount = width * height;
            var mask = EnsureBuffers(pixelCount).Mask;

            Vector4 matchColor = ReadSeedColor(dc, bounds, width, height, x, y, out int seedX, out int seedY);

            bool foregroundChanged = isFirst
                || !ColorWithinTolerance(matchColor, lastMatchColor, toleranceRaw)
                || toleranceRaw != lastForegroundTolerance
                || !lastBounds.Equals(bounds)
                || currentInput != input;

            int components;
            if (foregroundChanged)
            {
                PrepareColorMatchEffect(matchColor, toleranceRaw);
                var rendered = RenderForegroundToBuffer(dc, bounds, width, height);
                components = pipeline.Analyze(rendered.AsSpan(0, pixelCount), width, height);
                lastMatchColor = matchColor;
                lastForegroundTolerance = toleranceRaw;
                lastComponentCount = components;
            }
            else
            {
                components = lastComponentCount;
            }

            int seedIndex = ResolveSeedIndex(seedX, seedY, width, height);
            if (components == 0 || seedIndex < 0)
            {
                Array.Clear(mask, 0, pixelCount);
                EnsureFinalMaskBitmap(dc, width, height);
                finalMaskBitmap!.CopyFromMemory<int>(mask, width * 4);
                return TransformFinalMask(bounds);
            }

            bool maskChanged = pipeline.GenerateMask(
                seedIndex,
                (float)Math.Max(0, shapeThresholdRaw),
                invert,
                mask.AsSpan(0, pixelCount));

            if (!maskChanged && finalMaskBitmap is not null)
                return TransformFinalMask(bounds);

            EnsureFinalMaskBitmap(dc, width, height);
            finalMaskBitmap!.CopyFromMemory<int>(mask, width * 4);
            return TransformFinalMask(bounds);
        }

        int ResolveSeedIndex(int seedX, int seedY, int width, int height)
        {
            if ((uint)seedX >= (uint)width || (uint)seedY >= (uint)height)
                return -1;
            return pipeline.IsForeground(seedY * width + seedX) ? seedY * width + seedX : -1;
        }

        static bool ColorWithinTolerance(Vector4 a, Vector4 b, double toleranceRaw)
        {
            float epsilon = (float)(Math.Clamp(toleranceRaw, 0, 255) / 255.0) * 0.25f;
            return Math.Abs(a.X - b.X) <= epsilon
                && Math.Abs(a.Y - b.Y) <= epsilon
                && Math.Abs(a.Z - b.Z) <= epsilon
                && Math.Abs(a.W - b.W) <= epsilon;
        }

        ID2D1Image? TransformFinalMask(RawRectF bounds)
        {
            if (finalMaskTransform is null || finalMaskTransformOutput is null || finalMaskBitmap is null)
                return finalMaskBitmap;

            finalMaskTransform.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
            finalMaskTransform.SetInput(0, finalMaskBitmap, true);
            return finalMaskTransformOutput;
        }

        void PrepareColorMatchEffect(Vector4 targetColorVector, double toleranceRaw)
        {
            colorMatchEffect!.TargetColor = targetColorVector;
            colorMatchEffect.Tolerance = (float)Math.Clamp(toleranceRaw, 0, 255) / 255f;
            colorMatchEffect.Invert = 0f;
            colorMatchEffect.SetInput(0, input, true);
        }

        void UpdateAlphaMask(ID2D1Image maskSource, double blurRadius)
        {
            if (blurRadius > 0.001 && blurEffect is not null)
            {
                blurEffect.StandardDeviation = (float)blurRadius;
                blurEffect.SetInput(0, maskSource, true);
                using var blurredMask = blurEffect.Output;
                alphaMaskEffect!.SetInput(1, blurredMask, true);
            }
            else
            {
                blurEffect?.SetInput(0, null, true);
                alphaMaskEffect!.SetInput(1, maskSource, true);
            }
        }

        Vector4 ReadSeedColor(
            ID2D1DeviceContext dc,
            RawRectF bounds,
            int width,
            int height,
            double x,
            double y,
            out int seedX,
            out int seedY)
        {
            seedX = (int)Math.Round(x - bounds.Left);
            seedY = (int)Math.Round(y - bounds.Top);
            if ((uint)seedX >= (uint)width || (uint)seedY >= (uint)height) { seedX = seedY = 0; return default; }
            float sourceX = bounds.Left + seedX;
            float sourceY = bounds.Top + seedY;

            EnsureSeedBitmaps(dc);

            var previousTarget = dc.Target;
            dc.Target = seedBitmap;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(
                input!,
                new Vector2(-sourceX, -sourceY),
                null,
                InterpolationMode.NearestNeighbor,
                CompositeMode.SourceCopy);
            dc.EndDraw();
            dc.Target = previousTarget;

            seedStagingBitmap!.CopyFromBitmap(seedBitmap!);
            var mapped = seedStagingBitmap.Map(MapOptions.Read);
            try
            {
                Marshal.Copy(mapped.Bits, seedPixel, 0, 4);
            }
            finally
            {
                seedStagingBitmap.Unmap();
            }

            return new Vector4(
                seedPixel[2] / 255f,
                seedPixel[1] / 255f,
                seedPixel[0] / 255f,
                seedPixel[3] / 255f);
        }

        int[] RenderForegroundToBuffer(ID2D1DeviceContext dc, RawRectF bounds, int width, int height)
        {
            EnsureCandidateBitmaps(dc, width, height);
            var foreground = EnsureBuffers(width * height).Foreground;

            var previousTarget = dc.Target;
            dc.Target = candidateBitmap;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(
                colorMatchOutput!,
                new Vector2(-bounds.Left, -bounds.Top),
                null,
                InterpolationMode.NearestNeighbor,
                CompositeMode.SourceCopy);
            dc.EndDraw();
            dc.Target = previousTarget;

            candidateStagingBitmap!.CopyFromBitmap(candidateBitmap!);
            var mapped = candidateStagingBitmap.Map(MapOptions.Read);
            try
            {
                unsafe
                {
                    ForegroundExtractor.Extract((byte*)mapped.Bits, mapped.Pitch, width, height, foreground);
                }
            }
            finally
            {
                candidateStagingBitmap.Unmap();
            }

            return foreground;
        }

        void EnsureFinalMaskBitmap(ID2D1DeviceContext dc, int width, int height)
        {
            if (finalMaskBitmap is not null && finalMaskWidth == width && finalMaskHeight == height)
                return;

            disposer.RemoveAndDispose(ref finalMaskBitmap);
            var pixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            finalMaskBitmap = dc.CreateBitmap(
                new SizeI(width, height),
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.None));
            disposer.Collect(finalMaskBitmap);
            finalMaskWidth = width;
            finalMaskHeight = height;
        }

        void EnsureCandidateBitmaps(ID2D1DeviceContext dc, int width, int height)
        {
            if (candidateBitmap is not null
                && candidateStagingBitmap is not null
                && candidateWidth == width
                && candidateHeight == height)
                return;

            disposer.RemoveAndDispose(ref candidateBitmap);
            disposer.RemoveAndDispose(ref candidateStagingBitmap);

            var pixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var size = new SizeI(width, height);

            candidateBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.Target));
            disposer.Collect(candidateBitmap);

            candidateStagingBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
            disposer.Collect(candidateStagingBitmap);

            candidateWidth = width;
            candidateHeight = height;
        }

        void EnsureSeedBitmaps(ID2D1DeviceContext dc)
        {
            if (seedBitmap is not null && seedStagingBitmap is not null)
                return;

            var pixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var size = new SizeI(1, 1);

            seedBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.Target));
            disposer.Collect(seedBitmap);

            seedStagingBitmap = dc.CreateBitmap(
                size,
                new BitmapProperties1(pixelFormat, 96f, 96f, BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
            disposer.Collect(seedStagingBitmap);
        }

        (int[] Foreground, int[] Mask) EnsureBuffers(int pixelCount)
        {
            if (bufferPixelCount < pixelCount || foregroundBuffer is null || maskBuffer is null)
            {
                foregroundBuffer = new int[pixelCount];
                maskBuffer = new int[pixelCount];
                bufferPixelCount = pixelCount;
            }

            return (foregroundBuffer, maskBuffer);
        }
    }
}
