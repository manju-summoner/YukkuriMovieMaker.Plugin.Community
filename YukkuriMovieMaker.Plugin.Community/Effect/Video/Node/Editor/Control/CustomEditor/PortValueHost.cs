using System.ComponentModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor;

/// <summary>
///     YMM4標準の PropertyEditorAttribute2.SetBindings(control, item, propertyOwner, propertyInfo) は、
///     実在するオブジェクトの実在する CLR プロパティ（PropertyInfo で reflect できるもの）を要求する。
///     ノードエディタのポートの値は PortViewModel.CurrentValue という単なる object プロパティで
///     保持されているため、そのままでは渡せない。
///     このクラスは、ポートの値を「Value という名前の、ポートの ValueType と同じ型を持つ、
///     INotifyPropertyChanged 対応の実在プロパティ」として reflect できるようにするための
///     薄いアダプタ（ホスト）。item にも propertyOwner にもこのインスタンス自身を渡すことで、
///     PropertyEditorAttribute2 実装側からは通常のアイテムプロパティ編集と同じように振る舞う。
/// </summary>
public interface IPortValueHost
{
    /// <summary>
    ///     PortViewModel.PropertyChanged の購読を解除する。
    ///     コントロールがポートから切り離される際（DataContext変更・Unload）に必ず呼ぶこと。
    /// </summary>
    void Detach();
}

public sealed class PortValueHost<T> : IPortValueHost, INotifyPropertyChanged
{
    private readonly PortViewModel _port;

    public PortValueHost(PortViewModel port)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _port.PropertyChanged += OnPortPropertyChanged;
    }

    /// <summary>
    ///     ポートの現在値。PortViewModel.CurrentValue への読み書きをそのまま中継する。
    ///     プロパティ名・型（T）は固定であってはならず、必ず port.ValueType から
    ///     MakeGenericType で構築すること（PropertyEditorAttribute2 実装側が
    ///     PropertyInfo.PropertyType を見て挙動を変える可能性があるため）。
    /// </summary>
    public T? Value
    {
        get => _port.CurrentValue is T value ? value : default;
        set
        {
            if (Equals(Value, value))
                return;

            _port.CurrentValue = value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Detach()
    {
        _port.PropertyChanged -= OnPortPropertyChanged;
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // CurrentValue がグラフ側の都合（接続・Undo/Redo・他コントロールからの編集等）で
        // 外部から変化した場合も、必ずここで Value の変更通知を出す。
        // これを怠ると、ホストしているコントロール側の表示が古いままになる。
        if (e.PropertyName != nameof(PortViewModel.CurrentValue))
            return;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}