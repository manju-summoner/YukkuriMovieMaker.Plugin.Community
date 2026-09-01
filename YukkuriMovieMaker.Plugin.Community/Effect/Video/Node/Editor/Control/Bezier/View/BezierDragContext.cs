using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.View;

internal sealed class BezierDragContext
{
    public BezierDragContext(
        BezierHitType hitType,
        BezierNode node,
        Point mouseDownPosition)
    {
        HitType = hitType;
        Node = node;
        MouseDownPosition = mouseDownPosition;
        OriginalNodePosition = node.Position;
        OriginalInHandle = node.InHandle.Offset;
        OriginalOutHandle = node.OutHandle.Offset;
    }

    public BezierHitType HitType { get; }

    public BezierNode Node { get; }

    /// <summary>
    ///     ドラッグ開始時のマウス座標（画面座標）
    /// </summary>
    public Point MouseDownPosition { get; }

    /// <summary>
    ///     ドラッグ開始時のノード座標
    /// </summary>
    public Point OriginalNodePosition { get; }

    /// <summary>
    ///     ドラッグ開始時のInハンドル
    /// </summary>
    public Vector OriginalInHandle { get; }

    /// <summary>
    ///     ドラッグ開始時のOutハンドル
    /// </summary>
    public Vector OriginalOutHandle { get; }
}