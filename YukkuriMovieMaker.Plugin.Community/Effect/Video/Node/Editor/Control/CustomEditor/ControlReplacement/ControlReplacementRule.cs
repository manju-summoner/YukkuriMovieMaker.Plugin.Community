using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor.ControlReplacement;

public sealed class ControlReplacementRule
{
    public required string SourceTypeFullName { get; init; }
    public required string SourceValuePropertyName { get; init; }
    public required Func<PortControlBase> CreateReplacement { get; init; }
    public required DependencyProperty TargetValueProperty { get; init; }
    public Func<FrameworkElement, IValueConverter?>? ConverterFactory { get; init; }
    public bool SkipAutomaticValueBinding { get; init; }
    public Func<FrameworkElement, bool>? CanReplace { get; init; }
    public bool NotifyPreviewOnEachChange { get; init; }
    public Action<FrameworkElement, PortControlBase>? Configure { get; init; }
    public string? Description { get; init; }
}