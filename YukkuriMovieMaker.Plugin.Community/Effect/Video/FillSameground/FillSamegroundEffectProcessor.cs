using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSameground
{
    public class FillSamegroundProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly FillSamegroundEffect item;

        AffineTransform2D? outputEffect;
        ID2D1CommandList? brushCommandList;
        ID2D1CommandList? blendedCommandList;
        ID2D1CommandList? luminanceCommandList;

        IBrushSource? brushSource;
        readonly ID2D1SolidColorBrush transparentBrush;

        FillSamegroundCustomEffect? colorMatchEffect;
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

        byte[]? candidateBuffer;
        byte[]? maskBuffer;
        int bufferPixelCount;

        readonly Stack<int> fillStack = new(4096);

        bool isFirst = true;
        ID2D1Image? currentInput;
        Type? brushType;
        RawRectF lastBounds;
        FillSamegroundMode mode;
        double opacity, blur, tolerance, posX, posY;
        System.Windows.Media.Color targetColor;
        bool isInverted;

        public FillSamegroundProcessor(IGraphicsDevicesAndContext devices, FillSamegroundEffect item)
            : base(devices)
        {
            this.devices = devices;
            this.item = item;

            transparentBrush = devices.DeviceContext.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0f));
            disposer.Collect(transparentBrush);

            colorMatchEffect = new FillSamegroundCustomEffect(devices);
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

            var currentMode = item.Mode;
            var currentOpacity = item.Opacity.GetValue(frame, length, fps);
            var currentBlur = Math.Max(0, item.Blur.GetValue(frame, length, fps));
            var currentBlendMode = item.BlendMode;
            var currentIsInverted = item.IsInverted;
            var currentIsBrushOnly = item.IsBrushOnly;
            var currentPreserveLuminance = item.PreserveLuminance;
            var currentTolerance = item.Tolerance.GetValue(frame, length, fps);
            var currentTargetColor = item.TargetColor;
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
                || mode != currentMode
                || tolerance != currentTolerance
                || isInverted != currentIsInverted
                || currentMode == FillSamegroundMode.Position
                || currentMode == FillSamegroundMode.PositionColor
                || (UsesPosition(currentMode) && (posX != currentX || posY != currentY))
                || (currentMode == FillSamegroundMode.Color && targetColor != currentTargetColor);

            ID2D1Image? maskSource;
            if (needsMaskUpdate)
            {
                maskSource = currentMode switch
                {
                    FillSamegroundMode.Color => PrepareColorMask(currentTargetColor, currentTolerance, currentIsInverted),
                    FillSamegroundMode.PositionColor => PreparePositionColorMask(dc, bounds, width, height, currentX, currentY, currentTolerance, currentIsInverted),
                    _ => PrepareConnectedPositionMask(dc, bounds, width, height, currentX, currentY, currentTolerance, currentIsInverted),
                };
            }
            else
            {
                maskSource = currentMode switch
                {
                    FillSamegroundMode.Color or FillSamegroundMode.PositionColor => colorMatchOutput,
                    _ => finalMaskTransformOutput,
                };
            }

            if (maskSource is null)
            {
                outputEffect.SetInput(0, input, true);
                return effectDescription.DrawDescription;
            }

            if(needsMaskUpdate || currentBlur != blur)
                UpdateAlphaMask(maskSource, currentBlur);
            if(isFirst || currentOpacity != opacity)
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
            mode = currentMode;
            opacity = currentOpacity;
            blur = currentBlur;
            isInverted = currentIsInverted;
            tolerance = currentTolerance;
            targetColor = currentTargetColor;
            posX = currentX;
            posY = currentY;
            brushType = currentBrushType;

            if (UsesPosition(currentMode))
            {
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

            return effectDescription.DrawDescription;
        }

        ID2D1Image? PrepareColorMask(System.Windows.Media.Color color, double toleranceRaw, bool invert)
        {
            PrepareColorMatchEffect(ToPremultipliedVector(color), toleranceRaw, invert);
            return colorMatchOutput;
        }

        ID2D1Image? PreparePositionColorMask(
            ID2D1DeviceContext dc,
            RawRectF bounds,
            int width,
            int height,
            double x,
            double y,
            double toleranceRaw,
            bool invert)
        {
            var seedColor = ReadSeedColor(dc, bounds, width, height, x, y, out _, out _);
            PrepareColorMatchEffect(seedColor, toleranceRaw, invert);
            return colorMatchOutput;
        }

        ID2D1Image? PrepareConnectedPositionMask(
            ID2D1DeviceContext dc,
            RawRectF bounds,
            int width,
            int height,
            double x,
            double y,
            double toleranceRaw,
            bool invert)
        {
            var seedColor = ReadSeedColor(dc, bounds, width, height, x, y, out int seedX, out int seedY);
            PrepareColorMatchEffect(seedColor, toleranceRaw, invert: false);
            RenderCandidateMaskToBuffer(dc, bounds, width, height);

            EnsureBuffers(width * height);
            ComputeConnectedMaskInto(candidateBuffer!, width, height, seedX, seedY, invert, maskBuffer!, fillStack);

            EnsureFinalMaskBitmap(dc, width, height);
            finalMaskBitmap!.CopyFromMemory(maskBuffer!, width * 4);
            return TransformFinalMask(bounds);
        }

        ID2D1Image? TransformFinalMask(RawRectF bounds)
        {
            if (finalMaskTransform is null || finalMaskTransformOutput is null || finalMaskBitmap is null)
                return finalMaskBitmap;

            finalMaskTransform.TransformMatrix = Matrix3x2.CreateTranslation(bounds.Left, bounds.Top);
            finalMaskTransform.SetInput(0, finalMaskBitmap, true);
            return finalMaskTransformOutput;
        }

        void PrepareColorMatchEffect(Vector4 targetColorVector, double toleranceRaw, bool invert)
        {
            colorMatchEffect!.TargetColor = targetColorVector;
            colorMatchEffect.Tolerance = (float)Math.Clamp(toleranceRaw, 0, 255) / 255f;
            colorMatchEffect.Invert = invert ? 1f : 0f;
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

        void RenderCandidateMaskToBuffer(ID2D1DeviceContext dc, RawRectF bounds, int width, int height)
        {
            EnsureCandidateBitmaps(dc, width, height);
            EnsureBuffers(width * height);

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
                for (int row = 0; row < height; row++)
                    Marshal.Copy(mapped.Bits + row * mapped.Pitch, candidateBuffer!, row * width * 4, width * 4);
            }
            finally
            {
                candidateStagingBitmap.Unmap();
            }
        }

        static Vector4 ToPremultipliedVector(System.Windows.Media.Color color)
        {
            float a = color.A / 255f;
            return new Vector4(
                color.R / 255f * a,
                color.G / 255f * a,
                color.B / 255f * a,
                a);
        }

        static bool UsesPosition(FillSamegroundMode mode)
        {
            return mode == FillSamegroundMode.Position || mode == FillSamegroundMode.PositionColor;
        }

        static void ComputeConnectedMaskInto(
            byte[] candidates,
            int width,
            int height,
            int seedX,
            int seedY,
            bool invert,
            byte[] mask,
            Stack<int> stack)
        {
            int pixelCount = width * height;
            Array.Clear(mask, 0, pixelCount * 4);
            stack.Clear();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool IsCandidate(int x, int y) => candidates[(y * width + x) * 4 + 3] != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool IsFilled(int x, int y) => mask[(y * width + x) * 4 + 3] != 0;

            void FillRow(int y, int left, int right)
            {
                for (int x = left; x <= right; x++)
                {
                    int i = (y * width + x) * 4;
                    mask[i] = 255;
                    mask[i + 1] = 255;
                    mask[i + 2] = 255;
                    mask[i + 3] = 255;
                }
            }

            void PushSpansInRow(int y, int left, int right)
            {
                if ((uint)y >= (uint)height) return;
                int x = left;
                while (x <= right)
                {
                    while (x <= right && (!IsCandidate(x, y) || IsFilled(x, y))) x++;
                    if (x > right) break;
                    stack.Push(y * width + x);
                    while (x <= right && IsCandidate(x, y) && !IsFilled(x, y)) x++;
                }
            }

            if (IsCandidate(seedX, seedY))
                stack.Push(seedY * width + seedX);

            while (stack.Count > 0)
            {
                int packed = stack.Pop();
                int x = packed % width;
                int y = packed / width;

                if (IsFilled(x, y) || !IsCandidate(x, y))
                    continue;

                int left = x;
                while (left > 0 && IsCandidate(left - 1, y) && !IsFilled(left - 1, y)) left--;

                int right = x;
                while (right < width - 1 && IsCandidate(right + 1, y) && !IsFilled(right + 1, y)) right++;

                FillRow(y, left, right);
                PushSpansInRow(y - 1, left, right);
                PushSpansInRow(y + 1, left, right);
            }

            if (!invert)
                return;

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                bool filled = mask[bi + 3] != 0;
                byte value = filled ? (byte)0 : (byte)255;
                mask[bi] = value;
                mask[bi + 1] = value;
                mask[bi + 2] = value;
                mask[bi + 3] = value;
            }
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

        void EnsureBuffers(int pixelCount)
        {
            if (bufferPixelCount >= pixelCount && candidateBuffer is not null && maskBuffer is not null)
                return;

            candidateBuffer = new byte[pixelCount * 4];
            maskBuffer = new byte[pixelCount * 4];
            bufferPixelCount = pixelCount;
        }
    }
}
