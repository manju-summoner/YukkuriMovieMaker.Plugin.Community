using System.Windows.Data;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

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

        ControlRegistry.Register(typeof(NumberPort), (attr, port) =>
        {
            var numberAttr = (NumberPortControlAttribute)attr;

            var wrapper = new NumberPort
            {
                Min = numberAttr.Min,
                Max = numberAttr.Max,
                Digits = numberAttr.Digits,
                Unit = numberAttr.Unit,
                Default = numberAttr.Default
            };

            var binding = new Binding(nameof(NumberPort.Value))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(NumberPort.ValueProperty, binding);

            return wrapper;
        });
        ControlRegistry.Register(typeof(TextPort), (attr, port) =>
        {
            var textAttr = (TextPortControlAttribute)attr;

            var wrapper = new TextPort
            {
                Default = textAttr.Default
            };

            var binding = new Binding(nameof(TextPort.Value))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(TextPort.ValueProperty, binding);

            return wrapper;
        });
        ControlRegistry.Register(typeof(BoolPort), (attr, port) =>
        {
            var boolAttr = (BoolPortControlAttribute)attr;

            var wrapper = new BoolPort();

            var binding = new Binding(nameof(BoolPort.Value))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(BoolPort.ValueProperty, binding);

            if (boolAttr.Default)
                port.CurrentValue = boolAttr.Default;

            return wrapper;
        });
        ControlRegistry.Register(typeof(EnumPort), (attr, port) =>
        {
            var enumAttr = (EnumPortControlAttribute)attr;

            var wrapper = new EnumPort
            {
                Items = enumAttr.Items,
                IsEditable = enumAttr.IsEditable
            };

            var binding = new Binding(nameof(EnumPort.Value))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(EnumPort.ValueProperty, binding);

            port.CurrentValue ??= enumAttr.Default;

            return wrapper;
        });
        ControlRegistry.Register(typeof(FilePathPort), (attr, port) =>
        {
            var fileAttr = (FilePathPortControlAttribute)attr;

            var wrapper = new FilePathPort
            {
                AllowExtension = fileAttr.AllowExtension
            };

            var binding = new Binding(nameof(FilePathPort.Value))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(FilePathPort.ValueProperty, binding);

            port.CurrentValue ??= fileAttr.Default;

            return wrapper;
        });
        ControlRegistry.Register(typeof(ColorPort), (attr, port) =>
        {
            var colorAttr = (ColorPortControlAttribute)attr;

            var wrapper = new ColorPort
            {
                DefaultColor = colorAttr.DefaultColor
            };

            var binding = new Binding(nameof(ColorPort.SelectedColor))
            {
                Source = port.CurrentValue,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(ColorPort.SelectedColorProperty, binding);

            port.CurrentValue ??= colorAttr.DefaultColor;

            return wrapper;
        });
    }
}