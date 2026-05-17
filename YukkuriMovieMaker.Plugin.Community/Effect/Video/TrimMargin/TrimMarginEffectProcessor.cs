using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DCommon;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TrimMargin
{
    internal sealed class TrimMarginEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly TrimMarginEffect item;

        Crop? cropEffect;
        AffineTransform2D? transformEffect;
        ID2D1Image? currentInput;

        bool hasCache;
        ID2D1Image? cachedInput;
        Rect cachedBounds;
        (float Left, float Top, float Right, float Bottom)? cachedTrimRect;

        public TrimMarginEffectProcessor(IGraphicsDevicesAndContext devices, TrimMarginEffect item) : base(devices)
        {
            this.devices = devices;
            this.item = item;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || cropEffect is null || transformEffect is null || currentInput is null)
                return effectDescription.DrawDescription;

            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(currentInput);

            (float Left, float Top, float Right, float Bottom)? trimRect;
            if (hasCache && ReferenceEquals(cachedInput, currentInput) && cachedBounds.Equals(bounds))
            {
                trimRect = cachedTrimRect;
            }
            else
            {
                trimRect = ComputeTrimRect(dc, currentInput, bounds);
                cachedInput = currentInput;
                cachedBounds = bounds;
                cachedTrimRect = trimRect;
                hasCache = true;
            }

            if (!trimRect.HasValue)
                return effectDescription.DrawDescription;

            var (left, top, right, bottom) = trimRect.Value;

            cropEffect.Rectangle = new Vector4(left, top, right, bottom);

            transformEffect.TransformMatrix = item.Center
                ? new Matrix3x2(1f, 0f, 0f, 1f, -(left + right) * 0.5f, -(top + bottom) * 0.5f)
                : Matrix3x2.Identity;

            return effectDescription.DrawDescription;
        }

        private static (float Left, float Top, float Right, float Bottom)? ComputeTrimRect(
            ID2D1DeviceContext context, ID2D1Image image, Rect bounds)
        {
            int width = (int)MathF.Ceiling(bounds.Right - bounds.Left);
            int height = (int)MathF.Ceiling(bounds.Bottom - bounds.Top);

            if (width <= 0 || height <= 0)
                return null;

            var gpuProps = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                context.Dpi.Width,
                context.Dpi.Height,
                BitmapOptions.Target);

            var cpuProps = new BitmapProperties1(
                new PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                context.Dpi.Width,
                context.Dpi.Height,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);

            using var gpuBitmap = context.CreateBitmap(new SizeI(width, height), gpuProps);
            using var cpuBitmap = context.CreateBitmap(new SizeI(width, height), cpuProps);

            context.Target = gpuBitmap;
            context.BeginDraw();
            context.Clear(new Color4(0f, 0f, 0f, 0f));
            context.DrawImage(
                image,
                new Vector2(-bounds.Left, -bounds.Top),
                null,
                InterpolationMode.NearestNeighbor,
                CompositeMode.SourceCopy);
            context.EndDraw();
            context.Target = null;

            cpuBitmap.CopyFromBitmap(gpuBitmap);

            var map = cpuBitmap.Map(MapOptions.Read);
            try
            {
                return FindOpaqueBounds(map, width, height, bounds.Left, bounds.Top);
            }
            finally
            {
                cpuBitmap.Unmap();
            }
        }

        private static unsafe (float Left, float Top, float Right, float Bottom)? FindOpaqueBounds(
            MappedRectangle map, int width, int height, float originX, float originY)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            var ptr = (byte*)map.Bits;

            for (int y = 0; y < height; y++)
            {
                var row = ptr + (long)y * map.Pitch;
                for (int x = 0; x < width; x++)
                {
                    if (row[x * 4 + 3] == 0)
                        continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
                return null;

            return (
                originX + minX,
                originY + minY,
                originX + maxX + 1f,
                originY + maxY + 1f);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);

            transformEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transformEffect);

            using(var cropOutput = cropEffect.Output)
                transformEffect.SetInput(0, cropOutput, true);

            var output = transformEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            currentInput = input;
            cropEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            currentInput = null;
            cropEffect?.SetInput(0, null, true);
            transformEffect?.SetInput(0, null, true);
            hasCache = false;
            cachedInput = null;
            cachedTrimRect = null;
        }
    }
}
