using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor;

/// <summary>
///     コントロールの登録を行う初期化クラス
/// </summary>
public static class ControlRegistrations
{
    private static bool _initialized;

    /// <summary>
    ///     コントロールを登録
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        ControlRegistry.Register<NumberPort>(attr =>
        {
            var numberAttr = (NumberPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(NumberPort));

            factory.SetValue(NumberPort.MinProperty, numberAttr.Min);
            factory.SetValue(NumberPort.MaxProperty, numberAttr.Max);
            factory.SetValue(NumberPort.DigitsProperty, numberAttr.Digits);
            factory.SetValue(NumberPort.UnitProperty, numberAttr.Unit);
            factory.SetValue(NumberPort.DefaultProperty, numberAttr.Default);

            factory.SetBinding(NumberPort.ValueProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = 0f,
                    FallbackValue = 0f,
                    Converter = new ObjectToFloatConverter()
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register<TextPort>(attr =>
        {
            var textAttr = (TextPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextPort));

            factory.SetValue(TextPort.DefaultProperty, textAttr.Default);

            factory.SetBinding(TextPort.ValueProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = "",
                    FallbackValue = ""
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register<BoolPort>(attr =>
        {
            var boolAttr = (BoolPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(BoolPort));

            factory.SetValue(BoolPort.DefaultProperty, boolAttr.Default);
            factory.SetBinding(BoolPort.ValueProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = false,
                    FallbackValue = false
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register<EnumPort>(attr =>
        {
            var enumAttr = (EnumPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(EnumPort));

            factory.SetValue(EnumPort.ItemsProperty, enumAttr.Items);
            factory.SetValue(EnumPort.IsEditableProperty, enumAttr.IsEditable);
            factory.SetValue(EnumPort.DefaultProperty, enumAttr.Default);

            factory.SetBinding(EnumPort.ValueProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = 0,
                    FallbackValue = 0,
                    Converter = new EnumOrIntToIntConverter()
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register<FilePathPort>(attr =>
        {
            var fileAttr = (FilePathPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(FilePathPort));

            factory.SetValue(FilePathPort.AllowExtensionProperty, fileAttr.AllowExtension);
            factory.SetValue(FilePathPort.DefaultProperty, fileAttr.Default);

            factory.SetBinding(FilePathPort.ValueProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = "",
                    FallbackValue = ""
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register<ColorPort>(attr =>
        {
            var colorAttr = (ColorPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ColorPort));

            factory.SetValue(ColorPort.DefaultColorProperty, ColorStringConverter.ToColor(colorAttr.DefaultColor));

            factory.SetBinding(ColorPort.SelectedColorProperty,
                new Binding(nameof(PortViewModel.CurrentValue))
                {
                    Mode = BindingMode.OneWay,
                    TargetNullValue = Colors.White,
                    FallbackValue = Colors.White,
                    Converter = new ObjectToColorConverter()
                });
            factory.SetBinding(
                PortControlBase.BeginEditCommandProperty,
                new Binding(nameof(PortViewModel.BeginEditCommand)));

            factory.SetBinding(
                PortControlBase.EndEditCommandProperty,
                new Binding(nameof(PortViewModel.EndEditCommand)));

            template.VisualTree = factory;
            return template;
        });
    }
}