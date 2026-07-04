using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public readonly struct BezierSubdivisionResult
{
    public Point P01 { get; }
    public Point P12 { get; }
    public Point P23 { get; }

    public Point P012 { get; }
    public Point P123 { get; }

    public Point P0123 { get; }

    public BezierSegment Left { get; }

    public BezierSegment Right { get; }

    public BezierSubdivisionResult(
        Point p01,
        Point p12,
        Point p23,
        Point p012,
        Point p123,
        Point p0123,
        BezierSegment left,
        BezierSegment right)
    {
        P01 = p01;
        P12 = p12;
        P23 = p23;
        P012 = p012;
        P123 = p123;
        P0123 = p0123;
        Left = left;
        Right = right;
    }
}

public static class BezierEditingUtility
{
    public static void MoveNode(
        BezierCurve curve,
        BezierNode node,
        Point position)
    {
        position.X = BezierUtility.Clamp01(position.X);
        position.Y = BezierUtility.Clamp01(position.Y);

        var index = curve.Nodes.IndexOf(node);

        if (index < 0)
            return;

        if (node.IsFixed)
        {
            position = index == 0
                ? new Point(0, 0)
                : new Point(1, 1);
        }
        else
        {
            var minX = curve.Nodes[index - 1].Position.X;
            var maxX = curve.Nodes[index + 1].Position.X;

            position.X = Math.Clamp(position.X, minX, maxX);
        }

        node.Position = position;
    }

    public static void MoveInHandle(
        BezierNode node,
        Vector offset)
    {
        offset.X = Math.Min(offset.X, 0);

        node.InHandle.Offset = offset;

        if (node.Type == BezierNodeType.Smooth)
            MirrorToOut(node);
    }

    public static void MoveOutHandle(
        BezierNode node,
        Vector offset)
    {
        offset.X = Math.Max(offset.X, 0);

        node.OutHandle.Offset = offset;

        if (node.Type == BezierNodeType.Smooth)
            MirrorToIn(node);
    }

    public static void DeleteNode(
        BezierCurve curve,
        BezierNode node)
    {
        if (node.IsFixed)
            return;

        curve.Nodes.Remove(node);
    }

    public static void SetNodeType(
        BezierNode node,
        BezierNodeType type)
    {
        if (node.Type == type)
            return;

        node.Type = type;

        if (type == BezierNodeType.Smooth) MirrorToOut(node);
    }

    private static void MirrorToOut(BezierNode node)
    {
        var length = node.OutHandle.Offset.Length;

        if (length < 1e-8)
            length = node.InHandle.Offset.Length;

        if (length < 1e-8)
            length = 0.2;

        var v = -node.InHandle.Offset;

        if (v.Length > 1e-8)
        {
            v.Normalize();
            v *= length;
        }

        v.X = Math.Max(v.X, 0);

        node.OutHandle.Offset = v;
    }

    private static void MirrorToIn(BezierNode node)
    {
        var length = node.InHandle.Offset.Length;

        if (length < 1e-8)
            length = node.OutHandle.Offset.Length;

        if (length < 1e-8)
            length = 0.2;

        var v = -node.OutHandle.Offset;

        if (v.Length > 1e-8)
        {
            v.Normalize();
            v *= length;
        }

        v.X = Math.Min(v.X, 0);

        node.InHandle.Offset = v;
    }

    public static BezierNode InsertNode(
        BezierCurve curve,
        int segmentIndex,
        double t)
    {
        var leftNode = curve.Nodes[segmentIndex];
        var rightNode = curve.Nodes[segmentIndex + 1];

        var segment = new BezierSegment(
            leftNode.Position,
            leftNode.OutControlPoint,
            rightNode.InControlPoint,
            rightNode.Position);

        var result = BezierSubdivision.Split(segment, t);

        leftNode.OutHandle.Offset =
            result.P01 - leftNode.Position;

        rightNode.InHandle.Offset =
            result.P23 - rightNode.Position;

        var node = new BezierNode(result.P0123)
        {
            Type = BezierNodeType.Smooth
        };

        node.InHandle.Offset =
            result.P012 - result.P0123;

        node.OutHandle.Offset =
            result.P123 - result.P0123;

        curve.Nodes.Insert(segmentIndex + 1, node);

        return node;
    }
}