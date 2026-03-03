using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class NumberPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(NumberPort);

    /// <summary>
    ///     最小値
    /// </summary>
    public float Min { get; set; } = float.NaN;

    /// <summary>
    ///     最大値
    /// </summary>
    public float Max { get; set; } = float.NaN;

    /// <summary>
    ///     小数点以下の桁数
    /// </summary>
    public int Digits { get; set; } = 2;

    /// <summary>
    ///     単位
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>
    ///     デフォルト値
    /// </summary>
    public float Default { get; set; } = 0f;
}