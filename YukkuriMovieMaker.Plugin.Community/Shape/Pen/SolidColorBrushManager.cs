using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class SolidColorBrushManager : IDisposable
    {
        readonly List<ResourceItem<Color4, ID2D1SolidColorBrush>> resources = [];

        public void BeginUse()
        {
            foreach (var item in resources)
            {
                item.IsUsed = false;
            }
        }
        public ID2D1SolidColorBrush GetBrush(ID2D1DeviceContext6 dc, Color4 color)
        {
            var item = resources.FirstOrDefault(x => x.Key.Equals(color));
            if(item != null)
            {
                item.IsUsed = true;
                return item.Value;
            }

            var brush = dc.CreateSolidColorBrush(color);
            resources.Add(new ResourceItem<Color4, ID2D1SolidColorBrush>(color, brush));
            return brush;
        }
        public void EndUse()
        {
            foreach (var item in resources.Where(x => !x.IsUsed).ToList())
            {
                item.Dispose();
                resources.Remove(item);
            }
        }

        public void Dispose()
        {
            foreach (var item in resources)
            {
                item.Dispose();
            }
        }
    }
}
