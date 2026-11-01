using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
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
            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.Key.Equals(color))
                    continue;
                resource.IsUsed = true;
                return resource.Value;
            }

            var brush = dc.CreateSolidColorBrush(color);
            resources.Add(new ResourceItem<Color4, ID2D1SolidColorBrush>(color, brush));
            return brush;
        }
        public void EndUse()
        {
            for (var i = resources.Count - 1; i >= 0; i--)
            {
                var item = resources[i];
                if (item.IsUsed)
                    continue;
                item.Dispose();
                resources.RemoveAt(i);
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
