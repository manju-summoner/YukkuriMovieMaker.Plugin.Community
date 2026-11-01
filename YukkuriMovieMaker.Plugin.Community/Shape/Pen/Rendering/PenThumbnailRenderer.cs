using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    internal sealed class PenThumbnailRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly PenDrawResources resources = new();
        readonly Dictionary<Guid, PenLayerRenderer> renderers = [];

        ID2D1Bitmap1? target;
        ID2D1Bitmap1? staging;
        int width;
        int height;

        public void Update(IReadOnlyList<PenLayer> layers, int width, int height, double canvasWidth, double canvasHeight)
        {
            if (width <= 0 || height <= 0 || canvasWidth <= 0 || canvasHeight <= 0)
                return;

            RemoveUnusedRenderers(layers);
            EnsureResources(width, height);
            var targetBitmap = target;
            var stagingBitmap = staging;
            if (targetBitmap is null || stagingBitmap is null)
                return;

            var scale = (float)Math.Min(width / canvasWidth, height / canvasHeight);
            var transform =
                Matrix3x2.CreateScale(scale) *
                Matrix3x2.CreateTranslation(
                    (float)((width - canvasWidth * scale) / 2),
                    (float)((height - canvasHeight * scale) / 2));

            var dc = devices.DeviceContext;
            resources.BeginUse();

            foreach (var layer in layers)
            {
                if (layer.IsFolder)
                    continue;

                if (!renderers.TryGetValue(layer.Id, out var renderer))
                {
                    renderer = new PenLayerRenderer(devices);
                    renderers.Add(layer.Id, renderer);
                }

                var isStrokesChanged = renderer.SetStrokes(layer.Strokes, false);
                var isThumbnailValid = layer.Thumbnail is WriteableBitmap current
                    && current.PixelWidth == width && current.PixelHeight == height;
                if (!isStrokesChanged && isThumbnailValid)
                    continue;

                layer.Thumbnail = RenderLayer(dc, targetBitmap, stagingBitmap, renderer, layer, transform, width, height);
            }

            resources.EndUse();
        }

        WriteableBitmap RenderLayer(ID2D1DeviceContext6 dc, ID2D1Bitmap1 target, ID2D1Bitmap1 staging, PenLayerRenderer renderer, PenLayer layer, Matrix3x2 transform, int width, int height)
        {
            var bitmap = layer.Thumbnail as WriteableBitmap;
            if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);

            dc.Target = target;
            dc.BeginDraw();
            dc.Clear(new Color4(0, 0, 0, 0));
            dc.Transform = transform;
            renderer.Draw(dc, 0, renderer.TotalPointCount, 100, resources, null);
            dc.Transform = Matrix3x2.Identity;
            dc.EndDraw();
            dc.Target = null;

            staging.CopyFromBitmap(target);
            var mapped = staging.Map(MapOptions.Read);
            try
            {
                bitmap.Lock();
                CopyPixels(mapped, bitmap, width, height);
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
                staging.Unmap();
            }
            return bitmap;
        }

        static unsafe void CopyPixels(MappedRectangle mapped, WriteableBitmap bitmap, int width, int height)
        {
            var source = (byte*)mapped.Bits;
            var destination = (byte*)bitmap.BackBuffer;
            var sourceStride = mapped.Pitch;
            var destinationStride = bitmap.BackBufferStride;
            var rowBytes = (uint)(width * 4);
            for (var y = 0; y < height; y++)
                Buffer.MemoryCopy(source + (nint)y * sourceStride, destination + (nint)y * destinationStride, rowBytes, rowBytes);
        }

        void RemoveUnusedRenderers(IReadOnlyList<PenLayer> layers)
        {
            if (renderers.Count == 0)
                return;

            var alive = new HashSet<Guid>(layers.Count);
            foreach (var layer in layers)
                alive.Add(layer.Id);

            foreach (var id in renderers.Keys.ToList())
            {
                if (alive.Contains(id))
                    continue;
                renderers[id].Dispose();
                renderers.Remove(id);
            }
        }

        void EnsureResources(int width, int height)
        {
            if (this.width == width && this.height == height && target is not null && staging is not null)
                return;

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

            this.width = width;
            this.height = height;
        }

        public void Dispose()
        {
            foreach (var renderer in renderers.Values)
                renderer.Dispose();
            renderers.Clear();
            resources.Dispose();
            disposer.Dispose();
        }
    }
}
