using System.Numerics;
using System.Windows.Ink;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
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
            var tip = attributes.StylusTipTransform;
            var properties = new InkStyleProperties()
            {
                 NibShape = attributes.StylusTip is StylusTip.Ellipse ? InkNibShape.Round : InkNibShape.Square,
                 NibTransform = Matrix3x2.CreateScale((float)attributes.Width / (float)attributes.Height, 1f) * new Matrix3x2((float)tip.M11, (float)tip.M12, (float)tip.M21, (float)tip.M22, 0f, 0f),
            };

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!IsSameProperties(resource.Key, properties))
                    continue;
                resource.IsUsed = true;
                return resource.Value;
            }

            var inkStyle = dc.CreateInkStyle(properties);
            resources.Add(new ResourceItem<InkStyleProperties, ID2D1InkStyle>(properties, inkStyle));
            return inkStyle;
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

        static bool IsSameProperties(in InkStyleProperties left, in InkStyleProperties right)
        {
            return left.NibShape == right.NibShape
                && left.NibTransform.Equals(right.NibTransform);
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
