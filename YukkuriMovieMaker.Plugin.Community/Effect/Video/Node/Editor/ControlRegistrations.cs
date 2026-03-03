using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;
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

        ControlRegistry.Register(typeof(NumberPort), attr =>
        {
            var numberAttr = (NumberPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(NumberPort));

            factory.SetValue(NumberPort.MinProperty, numberAttr.Min);
            factory.SetValue(NumberPort.MaxProperty, numberAttr.Max);
            factory.SetValue(NumberPort.DigitsProperty, numberAttr.Digits);
            factory.SetValue(NumberPort.UnitProperty, numberAttr.Unit);
            factory.SetValue(NumberPort.DefaultProperty, numberAttr.Default);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = 0f,
                FallbackValue = 0f
            };
            factory.SetBinding(NumberPort.ValueProperty, binding);

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register(typeof(TextPort), attr =>
        {
            var textAttr = (TextPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextPort));

            factory.SetValue(TextPort.DefaultProperty, textAttr.Default);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = "",
                FallbackValue = ""
            };
            factory.SetBinding(TextPort.ValueProperty, binding);

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register(typeof(BoolPort), attr =>
        {
            var boolAttr = (BoolPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(BoolPort));

            factory.SetValue(BoolPort.ValueProperty, boolAttr.Default);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = false,
                FallbackValue = false
            };
            factory.SetBinding(BoolPort.ValueProperty, binding);

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register(typeof(EnumPort), attr =>
        {
            var enumAttr = (EnumPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(EnumPort));

            factory.SetValue(EnumPort.ItemsProperty, enumAttr.Items);
            factory.SetValue(EnumPort.IsEditableProperty, enumAttr.IsEditable);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = 0,
                FallbackValue = 0
            };
            factory.SetBinding(EnumPort.ValueProperty, binding);

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register(typeof(FilePathPort), attr =>
        {
            var fileAttr = (FilePathPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(FilePathPort));

            factory.SetValue(FilePathPort.AllowExtensionProperty, fileAttr.AllowExtension);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = "",
                FallbackValue = ""
            };
            factory.SetBinding(FilePathPort.ValueProperty, binding);

            template.VisualTree = factory;
            return template;
        });
        ControlRegistry.Register(typeof(ColorPort), attr =>
        {
            var colorAttr = (ColorPortControlAttribute)attr;
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ColorPort));

            factory.SetValue(ColorPort.DefaultColorProperty, colorAttr.DefaultColor);

            var binding = new Binding(nameof(PortViewModel.CurrentValue))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                TargetNullValue = Colors.White,
                FallbackValue = Colors.White
            };
            factory.SetBinding(ColorPort.SelectedColorProperty, binding);

            template.VisualTree = factory;
            return template;
        });
    }
}