using System.Numerics;
using Vortice;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    sealed class PenPencilRuns : IDisposable
    {
        const float LayerPaddingPixels = 16f;

        int[] starts;
        byte[] levels;
        ID2D1Ink?[] inks;
        RawRectF[] bounds;
        int[] builtStarts;
        int[] builtEnds;
        int runCount;
        int pointCount;

        double thickness;
        int first = -1;
        int last = -1;
        int visibleCount;
        RawRectF contentBounds;

        public PenPencilRuns(int capacity)
        {
            starts = new int[capacity];
            levels = new byte[capacity];
            inks = new ID2D1Ink?[capacity];
            bounds = new RawRectF[capacity];
            builtStarts = new int[capacity];
            builtEnds = new int[capacity];
        }

        public PenPencilRuns(byte[] pointLevels) : this(CountRuns(pointLevels))
        {
            foreach (var level in pointLevels)
                Append(level);
        }

        public void Append(byte level)
        {
            if (runCount == 0 || levels[runCount - 1] != level)
            {
                if (runCount == starts.Length)
                    Grow();
                starts[runCount] = pointCount;
                levels[runCount] = level;
                runCount++;
            }
            pointCount++;
        }

        public void Draw(ID2D1DeviceContext6 dc, PenStrokeGeometry geometry, int start, int end, double thickness, InkBezierSegment[] segments, ID2D1InkStyle inkStyle, PencilBrushManager brushes, System.Windows.Media.Color color)
        {
            Update(dc, geometry, start, end, thickness, segments, inkStyle);
            if (visibleCount == 0)
                return;

            var padding = LayerPaddingPixels / GetScale(dc.Transform);
            var parameters = new LayerParameters1
            {
                ContentBounds = new RawRectF(contentBounds.Left - padding, contentBounds.Top - padding, contentBounds.Right + padding, contentBounds.Bottom + padding),
                MaskAntialiasMode = AntialiasMode.Aliased,
                MaskTransform = Matrix3x2.Identity,
                Opacity = 1f,
                LayerOptions = LayerOptions1.None,
            };
            dc.PushLayer(ref parameters, null);
            dc.PrimitiveBlend = PrimitiveBlend.Max;
            for (var run = first; run <= last; run++)
            {
                if (inks[run] is { } ink)
                    dc.DrawInk(ink, brushes.GetBrush(dc, color, levels[run]), inkStyle);
            }
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            dc.PopLayer();
        }

        void Update(ID2D1DeviceContext6 dc, PenStrokeGeometry geometry, int start, int end, double thickness, InkBezierSegment[] segments, ID2D1InkStyle inkStyle)
        {
            if (this.thickness != thickness)
            {
                Release(0, runCount - 1);
                this.thickness = thickness;
            }

            var firstRun = FindRun(start);
            var lastRun = FindRun(end - 1);
            if (first >= 0)
            {
                Release(first, Math.Min(last, firstRun - 1));
                Release(Math.Max(first, lastRun + 1), last);
            }

            var scale = (float)thickness;
            visibleCount = 0;
            for (var run = firstRun; run <= lastRun; run++)
            {
                if (levels[run] == 0)
                    continue;

                var runStart = Math.Max(start, starts[run]);
                var runEnd = Math.Min(end, GetRunEnd(run));
                if (inks[run] is null || builtStarts[run] != runStart || builtEnds[run] != runEnd)
                {
                    inks[run]?.Dispose();
                    var ink = geometry.CreateInk(dc, runStart, runEnd, scale, segments);
                    inks[run] = ink;
                    bounds[run] = ink.GetBounds(inkStyle, null);
                    builtStarts[run] = runStart;
                    builtEnds[run] = runEnd;
                }

                contentBounds = visibleCount == 0 ? bounds[run] : Union(contentBounds, bounds[run]);
                visibleCount++;
            }

            first = firstRun;
            last = lastRun;
        }

        void Grow()
        {
            var capacity = Math.Max(1, starts.Length * 2);
            Array.Resize(ref starts, capacity);
            Array.Resize(ref levels, capacity);
            Array.Resize(ref inks, capacity);
            Array.Resize(ref bounds, capacity);
            Array.Resize(ref builtStarts, capacity);
            Array.Resize(ref builtEnds, capacity);
        }

        static int CountRuns(byte[] pointLevels)
        {
            var count = 0;
            for (var i = 0; i < pointLevels.Length; i++)
            {
                if (i == 0 || pointLevels[i] != pointLevels[i - 1])
                    count++;
            }
            return count;
        }

        static float GetScale(in Matrix3x2 transform)
        {
            var scale = MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
            return scale > 0 ? scale : 1f;
        }

        int GetRunEnd(int run) => run + 1 < runCount ? starts[run + 1] + 1 : pointCount;

        int FindRun(int pointIndex)
        {
            var found = Array.BinarySearch(starts, 0, runCount, pointIndex);
            return found >= 0 ? found : ~found - 1;
        }

        void Release(int fromRun, int toRun)
        {
            for (var run = fromRun; run <= toRun; run++)
            {
                inks[run]?.Dispose();
                inks[run] = null;
            }
        }

        static RawRectF Union(in RawRectF a, in RawRectF b) => new(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));

        public void Dispose()
        {
            Release(0, runCount - 1);
            first = -1;
            last = -1;
            visibleCount = 0;
        }
    }
}
