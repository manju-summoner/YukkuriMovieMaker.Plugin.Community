using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

/// <summary>
///     ノードの接続方法。
/// </summary>
public enum BezierNodeType
{
    /// <summary>
    ///     ハンドルが連動する。
    /// </summary>
    Smooth,

    /// <summary>
    ///     ハンドルが独立する。
    /// </summary>
    Corner
}

/// <summary>
///     ノードに対する制御点ハンドル。
/// </summary>
public sealed class BezierHandle
{
    public BezierHandle()
    {
    }

    public BezierHandle(Vector offset)
    {
        Offset = offset;
    }

    /// <summary>
    ///     ノードからの相対座標。
    /// </summary>
    public Vector Offset { get; set; }
}

/// <summary>
///     ベジェ曲線を構成するノード。
/// </summary>
public sealed class BezierNode
{
    public BezierNode()
    {
    }

    public BezierNode(Point position, bool isFixed = false)
    {
        Position = position;
        IsFixed = isFixed;
    }

    /// <summary>
    ///     ノード座標。
    /// </summary>
    public Point Position { get; set; }

    /// <summary>
    ///     入力側ハンドル。
    /// </summary>
    public BezierHandle InHandle { get; } = new();

    /// <summary>
    ///     出力側ハンドル。
    /// </summary>
    public BezierHandle OutHandle { get; } = new();

    /// <summary>
    ///     ノード種別。
    /// </summary>
    public BezierNodeType Type { get; set; } = BezierNodeType.Smooth;

    /// <summary>
    ///     始点または終点かどうか。
    /// </summary>
    public bool IsFixed { get; set; }

    /// <summary>
    ///     入力側制御点の絶対座標。
    /// </summary>
    public Point InControlPoint => Position + InHandle.Offset;

    /// <summary>
    ///     出力側制御点の絶対座標。
    /// </summary>
    public Point OutControlPoint => Position + OutHandle.Offset;
}

/// <summary>
///     2ノード間の3次ベジェ曲線。
/// </summary>
public readonly struct BezierSegment
{
    public Point P0 { get; }

    public Point P1 { get; }

    public Point P2 { get; }

    public Point P3 { get; }

    public BezierSegment(Point p0, Point p1, Point p2, Point p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
    }
}

/// <summary>
///     イージングベジェ曲線。
/// </summary>
public sealed class BezierCurve
{
    public BezierCurve()
    {
        Nodes.Add(new BezierNode(new Point(0, 0), true)
        {
            OutHandle =
            {
                Offset = new Vector(0.25, 0)
            }
        });

        Nodes.Add(new BezierNode(new Point(1, 1), true)
        {
            InHandle =
            {
                Offset = new Vector(-0.25, 0)
            }
        });
    }

    public IList<BezierNode> Nodes { get; } = new List<BezierNode>();

    public IEnumerable<BezierSegment> GetSegments()
    {
        for (var i = 0; i < Nodes.Count - 1; i++)
        {
            var a = Nodes[i];
            var b = Nodes[i + 1];

            yield return new BezierSegment(
                a.Position,
                a.OutControlPoint,
                b.InControlPoint,
                b.Position);
        }
    }

    public void AddNode(BezierNode node)
    {
        Nodes.Add(node);

        var sorted = Nodes
            .OrderBy(n => n.Position.X)
            .ToArray();

        Nodes.Clear();

        foreach (var n in sorted)
            Nodes.Add(n);
    }

    public void RemoveNode(BezierNode node)
    {
        if (node.IsFixed)
            throw new InvalidOperationException("固定ノードは削除できません。");

        Nodes.Remove(node);
    }

    public double Evaluate(double x)
    {
        return BezierEvaluator.Evaluate(this, x);
    }
}