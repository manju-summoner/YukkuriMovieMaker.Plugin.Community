using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    interface IPenGeometry : IDisposable
    {
        int PointCount { get; }

        int MaxSegmentCount { get; }

        void Draw(ID2D1DeviceContext6 dc, int start, int end, double thickness, InkBezierSegment[] segments, PenDrawResources resources);
    }
}
