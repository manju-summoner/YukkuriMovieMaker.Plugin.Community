using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class BezierPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(BezierPort);

    /// <summary>
    ///     デフォルト値（BezierSerializer が出力する形式の文字列）。
    ///     空文字列の場合、BezierPort.LinearDefault(直線)を用いる。
    /// </summary>
    public string Default { get; set; } = "";

    public override object GetDefaultValue()
    {
        return string.IsNullOrEmpty(Default) ? BezierPort.LinearDefault : Default;
    }
}