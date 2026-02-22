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

        ControlRegistry.Register("NumberPort", (attr, port) =>
        {
            var numberAttr = (NumberPortControlAttribute)attr;

            var wrapper = new NumberPortWrapper
            {
                Min = numberAttr.Min,
                Max = numberAttr.Max,
                Digits = numberAttr.Digits,
                Unit = numberAttr.Unit,
                Default = numberAttr.Default
            };

            var binding = new Binding("CurrentValue")
            {
                Source = port,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            wrapper.SetBinding(NumberPortWrapper.ValueProperty, binding);

            return wrapper;
        });
    }
}