using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public static class BezierUtility
{
    /// <summary>
    ///     3次ベジェ曲線上の点を求める。
    /// </summary>
    public static Point Evaluate(in BezierSegment segment, double t)
    {
        t = Clamp01(t);

        var u = 1.0 - t;

        var uu = u * u;
        var uuu = uu * u;

        var tt = t * t;
        var ttt = tt * t;

        return new Point(
            uuu * segment.P0.X +
            3 * uu * t * segment.P1.X +
            3 * u * tt * segment.P2.X +
            ttt * segment.P3.X,
            uuu * segment.P0.Y +
            3 * uu * t * segment.P1.Y +
            3 * u * tt * segment.P2.Y +
            ttt * segment.P3.Y);
    }

    /// <summary>
    ///     3次ベジェ曲線の一次導関数。
    /// </summary>
    public static Vector EvaluateDerivative(in BezierSegment segment, double t)
    {
        t = Clamp01(t);

        var u = 1.0 - t;

        return new Vector(
            3 * u * u * (segment.P1.X - segment.P0.X) +
            6 * u * t * (segment.P2.X - segment.P1.X) +
            3 * t * t * (segment.P3.X - segment.P2.X),
            3 * u * u * (segment.P1.Y - segment.P0.Y) +
            6 * u * t * (segment.P2.Y - segment.P1.Y) +
            3 * t * t * (segment.P3.Y - segment.P2.Y));
    }

    public static double Clamp01(double value)
    {
        if (value < 0)
            return 0;

        if (value > 1)
            return 1;

        return value;
    }
}

public static class BezierSubdivision
{
    public static BezierSubdivisionResult Split(
        in BezierSegment segment,
        double t)
    {
        var p01 = Lerp(segment.P0, segment.P1, t);
        var p12 = Lerp(segment.P1, segment.P2, t);
        var p23 = Lerp(segment.P2, segment.P3, t);

        var p012 = Lerp(p01, p12, t);
        var p123 = Lerp(p12, p23, t);

        var p0123 = Lerp(p012, p123, t);

        return new BezierSubdivisionResult(
            p01,
            p12,
            p23,
            p012,
            p123,
            p0123,
            new BezierSegment(segment.P0, p01, p012, p0123),
            new BezierSegment(p0123, p123, p23, segment.P3));
    }

    private static Point Lerp(Point a, Point b, double t)
    {
        return new Point(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t);
    }
}

public enum BezierHitType
{
    None,

    Node,

    InHandle,

    OutHandle,

    Segment
}

public sealed class BezierHitResult
{
    public BezierHitType HitType { get; init; }

    public BezierNode? Node { get; init; }

    public int SegmentIndex { get; init; } = -1;

    public double T { get; init; }
}

public static class BezierHitTester
{
    public static BezierHitResult HitTest(
        BezierCurve curve,
        IBezierCoordinateConverter converter,
        Point mouse,
        double nodeRadius,
        double handleRadius,
        double curveTolerance)
    {
        // Node
        foreach (var node in curve.Nodes)
        {
            var p = converter.ToScreen(node.Position);

            if ((p - mouse).Length <= nodeRadius)
                return new BezierHitResult
                {
                    HitType = BezierHitType.Node,
                    Node = node
                };
        }

        // Handle
        foreach (var node in curve.Nodes)
        {
            var p = converter.ToScreen(node.InControlPoint);

            if ((p - mouse).Length <= handleRadius)
                return new BezierHitResult
                {
                    HitType = BezierHitType.InHandle,
                    Node = node
                };

            p = converter.ToScreen(node.OutControlPoint);

            if ((p - mouse).Length <= handleRadius)
                return new BezierHitResult
                {
                    HitType = BezierHitType.OutHandle,
                    Node = node
                };
        }

        var index = 0;

        foreach (var segment in curve.GetSegments())
        {
            if (HitSegment(
                    segment,
                    converter,
                    mouse,
                    curveTolerance,
                    out var t))
                return new BezierHitResult
                {
                    HitType = BezierHitType.Segment,
                    SegmentIndex = index,
                    T = t
                };

            index++;
        }

        return new BezierHitResult
        {
            HitType = BezierHitType.None
        };
    }

    private static bool HitSegment(
        BezierSegment segment,
        IBezierCoordinateConverter converter,
        Point mouse,
        double tolerance,
        out double t)
    {
        const int Samples = 100;

        var bestDistance = double.MaxValue;
        double bestT = 0;

        for (var i = 0; i <= Samples; i++)
        {
            var tt = i / (double)Samples;

            var p = converter.ToScreen(
                BezierUtility.Evaluate(segment, tt));

            var distance = (p - mouse).Length;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestT = tt;
            }
        }

        t = bestT;

        return bestDistance <= tolerance;
    }
}