namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public static class BezierMonotonicAnalyzer
{
    public static List<(double t0, double t1)> FindNonMonotonicXRegions(
        BezierSegment segment,
        int samples = 64)
    {
        var result = new List<(double, double)>();

        var inRegion = false;
        double start = 0;

        var prev = BezierUtility.Evaluate(segment, 0);

        for (var i = 1; i <= samples; i++)
        {
            var t = i / (double)samples;
            var p = BezierUtility.Evaluate(segment, t);

            var violates = p.X < prev.X;

            if (violates && !inRegion)
            {
                inRegion = true;
                start = (i - 1) / (double)samples;
            }
            else if (!violates && inRegion)
            {
                inRegion = false;
                result.Add((start, t));
            }

            prev = p;
        }

        if (inRegion)
            result.Add((start, 1));

        return result;
    }
}