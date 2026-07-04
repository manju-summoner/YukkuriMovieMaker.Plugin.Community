using System.Globalization;
using System.Text;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public static class BezierSerializer
{
    public static string Serialize(BezierCurve curve)
    {
        var sb = new StringBuilder();

        sb.Append("V1|");

        var first = true;

        foreach (var node in curve.Nodes)
        {
            if (!first)
                sb.Append(';');

            first = false;

            sb.Append(node.Position.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.Position.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.InHandle.Offset.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.InHandle.Offset.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.OutHandle.Offset.X.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.OutHandle.Offset.Y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(node.Type == BezierNodeType.Smooth ? "S" : "C");
        }

        return sb.ToString();
    }
}

public static class BezierParser
{
    public static BezierCurve Deserialize(string text)
    {
        var curve = new BezierCurve();

        curve.Nodes.Clear();

        if (string.IsNullOrWhiteSpace(text))
        {
            AddFixedNodes(curve);
            return curve;
        }

        if (text.StartsWith("V1|"))
            text = text[3..];

        foreach (var record in text.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(record))
                continue;

            var values = record.Split(',');

            if (values.Length != 7)
                continue;

            if (!TryParse(values, out var node))
                continue;

            curve.Nodes.Add(node);
        }

        EnsureFixedNodes(curve);

        Sort(curve);

        return curve;
    }

    private static bool TryParse(string[] values, out BezierNode node)
    {
        node = new BezierNode();

        if (!double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            return false;

        if (!double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return false;

        if (!double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ix))
            return false;

        if (!double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var iy))
            return false;

        if (!double.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var ox))
            return false;

        if (!double.TryParse(values[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var oy))
            return false;

        node.Position = new Point(x, y);

        node.InHandle.Offset = new Vector(ix, iy);

        node.OutHandle.Offset = new Vector(ox, oy);

        node.Type = values[6] == "C"
            ? BezierNodeType.Corner
            : BezierNodeType.Smooth;

        return true;
    }

    private static void EnsureFixedNodes(BezierCurve curve)
    {
        var hasStart = false;
        var hasEnd = false;

        foreach (var node in curve.Nodes)
        {
            if (node.Position == new Point(0, 0))
            {
                node.IsFixed = true;
                hasStart = true;
            }

            if (node.Position == new Point(1, 1))
            {
                node.IsFixed = true;
                hasEnd = true;
            }
        }

        if (!hasStart)
            curve.Nodes.Insert(0,
                new BezierNode(new Point(0, 0), true)
                {
                    OutHandle =
                    {
                        Offset = new Vector(0.25, 0)
                    }
                });

        if (!hasEnd)
            curve.Nodes.Add(
                new BezierNode(new Point(1, 1), true)
                {
                    InHandle =
                    {
                        Offset = new Vector(-0.25, 0)
                    }
                });
    }

    private static void AddFixedNodes(BezierCurve curve)
    {
        curve.Nodes.Add(new BezierNode(new Point(0, 0), true)
        {
            OutHandle =
            {
                Offset = new Vector(0.25, 0)
            }
        });

        curve.Nodes.Add(new BezierNode(new Point(1, 1), true)
        {
            InHandle =
            {
                Offset = new Vector(-0.25, 0)
            }
        });
    }

    private static void Sort(BezierCurve curve)
    {
        var sorted = curve.Nodes
            .OrderBy(x => x.Position.X)
            .ToList();

        curve.Nodes.Clear();

        foreach (var node in sorted)
            curve.Nodes.Add(node);
    }
}