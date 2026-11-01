using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    internal sealed class PenPreviewRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly DisposeCollector disposer = new();

        ID2D1Bitmap1? target;
        ID2D1Bitmap1? staging;
        WriteableBitmap? bitmap;
        int width;
        int height;
        double dpi;

        public WriteableBitmap Render(ID2D1Image image, int width, int height)
        {
            EnsureResources(width, height, 96);
            if (target is null || staging is null || bitmap is null)
                throw new InvalidOperationException("preview resources are not ready.");

            var dc = devices.DeviceContext;
            dc.Target = target;
            dc.BeginDraw();
            dc.Clear(new Color4(0, 0, 0, 0));
            dc.DrawImage(image, new Vector2(width / 2f, height / 2f));
            dc.EndDraw();
            dc.Target = null;
            return Copy(new Int32Rect(0, 0, width, height));
        }

        public WriteableBitmap RenderView(ID2D1Image image, int width, int height, double scale, Point origin, double canvasWidth, double canvasHeight, double dpi, Int32Rect? dirty)
        {
            var isFresh = EnsureResources(width, height, dpi);
            if (target is null || staging is null || bitmap is null)
                throw new InvalidOperationException("preview resources are not ready.");

            var region = dirty is { } rect && !isFresh ? rect : new Int32Rect(0, 0, width, height);
            var halfWidth = (float)(canvasWidth / 2 * scale);
            var halfHeight = (float)(canvasHeight / 2 * scale);
            var dc = devices.DeviceContext;
            dc.Target = target;
            dc.BeginDraw();
            dc.PushAxisAlignedClip(new Vortice.RawRectF(region.X, region.Y, region.X + region.Width, region.Y + region.Height), AntialiasMode.Aliased);
            dc.Clear(new Color4(0, 0, 0, 0));
            dc.Transform = Matrix3x2.CreateTranslation((float)origin.X + halfWidth, (float)origin.Y + halfHeight);
            dc.PushAxisAlignedClip(new Vortice.RawRectF(-halfWidth, -halfHeight, halfWidth, halfHeight), AntialiasMode.Aliased);
            dc.DrawImage(image, Vector2.Zero);
            dc.PopAxisAlignedClip();
            dc.Transform = Matrix3x2.Identity;
            dc.PopAxisAlignedClip();
            dc.EndDraw();
            dc.Target = null;
            return Copy(region);
        }

        WriteableBitmap Copy(Int32Rect region)
        {
            if (target is null || staging is null || bitmap is null)
                throw new InvalidOperationException("preview resources are not ready.");

            var origin = new Int2(region.X, region.Y);
            staging.CopyFromBitmap(origin, target, new RectI(region.X, region.Y, region.Width, region.Height));
            var mapped = staging.Map(MapOptions.Read);
            try
            {
                bitmap.Lock();
                CopyPixels(mapped, bitmap, region);
                bitmap.AddDirtyRect(region);
            }
            finally
            {
                bitmap.Unlock();
                staging.Unmap();
            }
            return bitmap;
        }

        static unsafe void CopyPixels(MappedRectangle mapped, WriteableBitmap bitmap, Int32Rect region)
        {
            var source = (byte*)mapped.Bits + (nint)region.Y * mapped.Pitch + region.X * 4;
            var destination = (byte*)bitmap.BackBuffer + (nint)region.Y * bitmap.BackBufferStride + region.X * 4;
            var sourceStride = mapped.Pitch;
            var destinationStride = bitmap.BackBufferStride;
            var rowBytes = (uint)(region.Width * 4);
            for (var y = 0; y < region.Height; y++)
                Buffer.MemoryCopy(source + (nint)y * sourceStride, destination + (nint)y * destinationStride, rowBytes, rowBytes);
        }

        bool EnsureResources(int width, int height, double dpi)
        {
            if (this.width == width && this.height == height && this.dpi == dpi && target is not null && staging is not null && bitmap is not null)
                return false;

            if (target is not null)
                disposer.RemoveAndDispose(ref target);
            if (staging is not null)
                disposer.RemoveAndDispose(ref staging);

            var dc = devices.DeviceContext;
            var format = new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var size = new SizeI(width, height);
            target = dc.CreateBitmap(size, new BitmapProperties1(format, 96, 96, BitmapOptions.Target));
            disposer.Collect(target);
            staging = dc.CreateBitmap(size, new BitmapProperties1(format, 96, 96, BitmapOptions.CannotDraw | BitmapOptions.CpuRead));
            disposer.Collect(staging);
            bitmap = new WriteableBitmap(width, height, dpi, dpi, System.Windows.Media.PixelFormats.Pbgra32, null);

            this.width = width;
            this.height = height;
            this.dpi = dpi;
            return true;
        }

        public void Dispose()
        {
            disposer.Dispose();
        }
    }
}
