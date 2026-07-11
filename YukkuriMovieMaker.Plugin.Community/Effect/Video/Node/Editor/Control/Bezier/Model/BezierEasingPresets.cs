using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

/// <summary>
///     名前付きイージング関数のプリセット定義。
///     値はCSS Easing Functions(cubic-bezier)として広く公開・共有されている標準的な制御点に基づく。
///     P1・P2はそれぞれ cubic-bezier(x1, y1, x2, y2) の (x1, y1)・(x2, y2) に対応する絶対座標であり、
///     始点(0,0)・終点(1,1)からの相対オフセットは Apply 内で算出する。
/// </summary>
/// <remarks>
///     Bounce・Elasticは単一の3次ベジェ曲線では複数回の振動・反発を再現できないため、
///     このプリセット一覧には含めていない。CSSのcubic-bezier()自体もこれらを提供しない。
/// </remarks>
public static class BezierEasingPresets
{
    public static readonly IReadOnlyList<BezierEasingPreset> All =
    [
        new("linear", new Point(0.25, 0.25), new Point(0.75, 0.75)),

        new("easeInSine", new Point(0.12, 0), new Point(0.39, 0)),
        new("easeOutSine", new Point(0.61, 1), new Point(0.88, 1)),
        new("easeInOutSine", new Point(0.37, 0), new Point(0.63, 1)),

        new("easeInQuad", new Point(0.11, 0), new Point(0.5, 0)),
        new("easeOutQuad", new Point(0.5, 1), new Point(0.89, 1)),
        new("easeInOutQuad", new Point(0.45, 0), new Point(0.55, 1)),

        new("easeInCubic", new Point(0.32, 0), new Point(0.67, 0)),
        new("easeOutCubic", new Point(0.33, 1), new Point(0.68, 1)),
        new("easeInOutCubic", new Point(0.65, 0), new Point(0.35, 1)),

        new("easeInQuart", new Point(0.5, 0), new Point(0.75, 0)),
        new("easeOutQuart", new Point(0.25, 1), new Point(0.5, 1)),
        new("easeInOutQuart", new Point(0.76, 0), new Point(0.24, 1)),

        new("easeInQuint", new Point(0.64, 0), new Point(0.78, 0)),
        new("easeOutQuint", new Point(0.22, 1), new Point(0.36, 1)),
        new("easeInOutQuint", new Point(0.83, 0), new Point(0.17, 1)),

        new("easeInExpo", new Point(0.7, 0), new Point(0.84, 0)),
        new("easeOutExpo", new Point(0.16, 1), new Point(0.3, 1)),
        new("easeInOutExpo", new Point(0.87, 0), new Point(0.13, 1)),

        new("easeInCirc", new Point(0.55, 0), new Point(1, 0.45)),
        new("easeOutCirc", new Point(0, 0.55), new Point(0.45, 1)),
        new("easeInOutCirc", new Point(0.85, 0), new Point(0.15, 1)),

        new("easeInBack", new Point(0.36, 0), new Point(0.66, -0.56)),
        new("easeOutBack", new Point(0.34, 1.56), new Point(0.64, 1)),
        new("easeInOutBack", new Point(0.68, -0.6), new Point(0.32, 1.6))
    ];

    /// <summary>
    ///     指定したプリセットの制御点を曲線に適用する。
    ///     固定ノード(始点・終点)以外の中間ノードは全て削除し、
    ///     2ノードの単純なプリセット曲線として再構築する。
    /// </summary>
    public static void Apply(BezierCurve curve, BezierEasingPreset preset)
    {
        var extraNodes = curve.Nodes.Where(n => !n.IsFixed).ToList();
        foreach (var node in extraNodes)
            curve.RemoveNode(node);

        var start = curve.Nodes.First(n => n.Position == new Point(0, 0));
        var end = curve.Nodes.First(n => n.Position == new Point(1, 1));

        start.Type = BezierNodeType.Corner;
        end.Type = BezierNodeType.Corner;

        start.OutHandle.Offset = preset.P1 - new Point(0, 0);
        end.InHandle.Offset = preset.P2 - new Point(1, 1);
    }
}

public sealed record BezierEasingPreset(string Name, Point P1, Point P2);