using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    class PencilBrushManager : IDisposable
    {
        const int RetainedColorCount = 8;

        static readonly Vortice.DCommon.PixelFormat GrainFormat = new(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);

        readonly List<ResourceItem<System.Windows.Media.Color, ID2D1Bitmap1>> bitmaps = [];
        readonly List<ResourceItem<(System.Windows.Media.Color Color, int Level), ID2D1BitmapBrush1>> brushes = [];

        public void BeginUse()
        {
            foreach (var item in bitmaps)
                item.IsUsed = false;
        }

        public ID2D1BitmapBrush1 GetBrush(ID2D1DeviceContext6 dc, System.Windows.Media.Color color, int level)
        {
            var rgb = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
            var bitmap = GetBitmap(dc, rgb);
            var key = (rgb, level);
            for (var i = 0; i < brushes.Count; i++)
            {
                var resource = brushes[i];
                if (resource.Key.Equals(key))
                    return resource.Value;
            }

            var brush = dc.CreateBitmapBrush(
                bitmap,
                new BitmapBrushProperties1(ExtendMode.Wrap, ExtendMode.Wrap, InterpolationMode.Linear),
                new BrushProperties(PenPencil.GetOpacity(level), Matrix3x2.Identity));
            brushes.Add(new ResourceItem<(System.Windows.Media.Color, int), ID2D1BitmapBrush1>(key, brush));
            return brush;
        }

        ID2D1Bitmap1 GetBitmap(ID2D1DeviceContext6 dc, System.Windows.Media.Color color)
        {
            for (var i = 0; i < bitmaps.Count; i++)
            {
                var resource = bitmaps[i];
                if (resource.Key != color)
                    continue;
                resource.IsUsed = true;
                return resource.Value;
            }

            var bitmap = dc.CreateBitmap(new SizeI(PenPencil.GrainSize, PenPencil.GrainSize), new BitmapProperties1(GrainFormat, 96f, 96f, BitmapOptions.None));
            bitmap.CopyFromMemory(PenPencil.CreatePixels(color), PenPencil.GrainSize * 4);
            bitmaps.Add(new ResourceItem<System.Windows.Media.Color, ID2D1Bitmap1>(color, bitmap));
            return bitmap;
        }

        public void EndUse()
        {
            if (bitmaps.Count <= RetainedColorCount)
                return;

            for (var i = bitmaps.Count - 1; i >= 0; i--)
            {
                var item = bitmaps[i];
                if (item.IsUsed)
                    continue;
                ReleaseBrushes(item.Key);
                item.Dispose();
                bitmaps.RemoveAt(i);
            }
        }

        void ReleaseBrushes(System.Windows.Media.Color color)
        {
            for (var i = brushes.Count - 1; i >= 0; i--)
            {
                var item = brushes[i];
                if (item.Key.Color != color)
                    continue;
                item.Dispose();
                brushes.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            foreach (var item in brushes)
                item.Dispose();
            foreach (var item in bitmaps)
                item.Dispose();
        }
    }
}
