using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class InkResourceManager : IDisposable
    {
        readonly List<ResourceItem<InkPoint[], ID2D1Ink>> resources = [];

        public void BeginUse()
        {
            foreach (var item in resources)
            {
                item.IsUsed = false;
            }
        }
        public ID2D1Ink GetInk(ID2D1DeviceContext6 dc, InkPoint[] points)
        {
            var item = resources.FirstOrDefault(x => x.Key.Length == points.Length && x.Key.Zip(points).All(pair => pair.First.Equals(pair.Second)));
            if(item != null)
            {
                item.IsUsed = true;
                return item.Value;
            }

            var ink = dc.CreateInk(points[0]);
            var segments =
                points[1..]
                .Chunk(3)
                .Select(x =>
                    new InkBezierSegment()
                    { 
                        Point1 = x[0],
                        Point2 = x.Length > 1 ? x[1] : x[^1],
                        Point3 = x.Length > 2 ? x[2] : x[^1], 
                    })
                .DefaultIfEmpty(
                    new InkBezierSegment()
                    {
                        Point1 = points[0],
                        Point2 = points[0],
                        Point3 = points[0],
                    })
                .ToArray();
            ink.AddSegments(segments, segments.Length);
            
            resources.Add(new ResourceItem<InkPoint[], ID2D1Ink>(points, ink));
            return ink;
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
