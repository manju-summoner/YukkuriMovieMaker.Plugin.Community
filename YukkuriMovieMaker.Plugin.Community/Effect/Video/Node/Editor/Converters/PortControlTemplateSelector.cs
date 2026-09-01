using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

/// <summary>
///     ポートの属性に基づいて適切なDataTemplateを選択
/// </summary>
public class PortControlTemplateSelector : DataTemplateSelector
{
    /// <summary>
    ///     NumberPort等、固定に実装されているコントロール以外（IPropertyEditorControl を実装し
    ///     PropertyEditorAttribute2 で構成される任意のコントロール）をポートに表示するための、
    ///     使い回し可能な単一のDataTemplate。
    ///     属性の型ごとに個別のコントロール型・DataTemplateを用意する必要はなく、
    ///     CustomEditorPort が DataContext（PortViewModel.EditorAttribute）を見てその場で生成する。
    /// </summary>
    private static readonly DataTemplate CustomEditorPortTemplate = CreateCustomEditorPortTemplate();

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not PortViewModel port)
            return null;

        if (!port.HasControl)
            return null;

        if (port.ControlAttribute != null)
            return ControlRegistry.CreateControl(port.ControlAttribute);

        if (port.EditorAttribute != null)
            return CustomEditorPortTemplate;

        return null;
    }

    private static DataTemplate CreateCustomEditorPortTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(CustomEditorPort));

        factory.SetBinding(
            PortControlBase.BeginEditCommandProperty,
            new Binding(nameof(PortViewModel.BeginEditCommand)));

        factory.SetBinding(
            PortControlBase.EndEditCommandProperty,
            new Binding(nameof(PortViewModel.EndEditCommand)));

        template.VisualTree = factory;
        return template;
    }
}