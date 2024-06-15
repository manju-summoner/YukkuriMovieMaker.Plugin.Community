using System.Numerics;
using System.Windows.Ink;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class InkStyleResourceManager : IDisposable
    {
        readonly List<ResourceItem<InkStyleProperties, ID2D1InkStyle>> resources = [];

        public void BeginUse()
        {
            foreach (var item in resources)
            {
                item.IsUsed = false;
            }
        }
        public ID2D1InkStyle GetInkStyle(ID2D1DeviceContext6 dc, DrawingAttributes attributes)
        {
            var properties = new InkStyleProperties()
            {
                 NibShape = attributes.StylusTip is StylusTip.Ellipse ? InkNibShape.Round : InkNibShape.Square,
                 NibTransform = Matrix3x2.CreateScale((float)attributes.Width / (float)attributes.Height, 1f),
            };

            var item = resources.FirstOrDefault(x => x.Key.Equals(properties));
            if(item != null)
            {
                item.IsUsed = true;
                return item.Value;
            }

            var inkStyle = dc.CreateInkStyle(properties);
            resources.Add(new ResourceItem<InkStyleProperties, ID2D1InkStyle>(properties, inkStyle));
            return inkStyle;
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
