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

    /// <summary>
    ///     入力側ハンドルを移動する。
    ///     表示上のハンドル位置はクランプしない。S字防止のための制約は
    ///     評価時(BezierEvaluator)側で内部的にのみ適用され、見た目のハンドル位置には影響しない。
    ///     先頭の固定ノード(0,0)は左側に接続を持たないため、InHandleは常に無効(移動不可)。
    /// </summary>
    public static void MoveInHandle(
        BezierCurve curve,
        BezierNode node,
        Vector offset)
    {
        var index = curve.Nodes.IndexOf(node);

        if (index == 0)
            return;

        node.InHandle.Offset = offset;

        if (node.Type == BezierNodeType.Smooth)
            MirrorToOut(curve, node);
    }

    /// <summary>
    ///     出力側ハンドルを移動する。
    ///     表示上のハンドル位置はクランプしない。S字防止のための制約は
    ///     評価時(BezierEvaluator)側で内部的にのみ適用され、見た目のハンドル位置には影響しない。
    ///     末尾の固定ノード(1,1)は右側に接続を持たないため、OutHandleは常に無効(移動不可)。
    /// </summary>
    public static void MoveOutHandle(
        BezierCurve curve,
        BezierNode node,
        Vector offset)
    {
        var index = curve.Nodes.IndexOf(node);

        if (index == curve.Nodes.Count - 1)
            return;

        node.OutHandle.Offset = offset;

        if (node.Type == BezierNodeType.Smooth)
            MirrorToIn(curve, node);
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
        BezierCurve curve,
        BezierNode node,
        BezierNodeType type)
    {
        if (node.Type == type)
            return;

        node.Type = type;

        if (type == BezierNodeType.Smooth) MirrorToOut(curve, node);
    }

    private static void MirrorToOut(BezierCurve curve, BezierNode node)
    {
        var index = curve.Nodes.IndexOf(node);

        if (index == 0)
            return;

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

        node.OutHandle.Offset = v;
    }

    private static void MirrorToIn(BezierCurve curve, BezierNode node)
    {
        var index = curve.Nodes.IndexOf(node);

        if (index == curve.Nodes.Count - 1)
            return;

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
            Type = BezierNodeType.Smooth,
            InHandle =
            {
                Offset = result.P012 - result.P0123
            },
            OutHandle =
            {
                Offset = result.P123 - result.P0123
            }
        };

        curve.Nodes.Insert(segmentIndex + 1, node);

        return node;
    }
}