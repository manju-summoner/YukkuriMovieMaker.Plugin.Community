namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public static class BezierEvaluator
{
    /// <summary>
    ///     時間(0～1)から値(0～1)を取得する。
    /// </summary>
    public static double Evaluate(BezierCurve curve, double x)
    {
        x = BezierUtility.Clamp01(x);

        var nodes = curve.Nodes;

        if (nodes.Count < 2)
            return x;

        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var a = nodes[i];
            var b = nodes[i + 1];

            if (x > b.Position.X)
                continue;

            var segment = new BezierSegment(
                a.Position,
                a.OutControlPoint,
                b.InControlPoint,
                b.Position);

            return EvaluateSegment(segment, x);
        }

        return 1.0;
    }

    private static double EvaluateSegment(in BezierSegment segment, double x)
    {
        var t = GuessInitialT(segment, x);

        for (var i = 0; i < 8; i++)
        {
            var p = BezierUtility.Evaluate(segment, t);
            var d = BezierUtility.EvaluateDerivative(segment, t);

            if (Math.Abs(d.X) < 1e-8)
                break;

            t -= (p.X - x) / d.X;
            t = BezierUtility.Clamp01(t);
        }

        return BezierUtility.Evaluate(segment, t).Y;
    }

    private static double GuessInitialT(in BezierSegment segment, double x)
    {
        var width = segment.P3.X - segment.P0.X;

        if (width <= 1e-8)
            return 0;

        return BezierUtility.Clamp01((x - segment.P0.X) / width);
    }
}