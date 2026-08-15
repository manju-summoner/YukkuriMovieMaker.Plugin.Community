using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class PortViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;
    internal readonly Guid NodeId;

    private object? _currentValue;
    private IEditorInfo? _editorInfo;

    public PortViewModel(
        string name,
        string label,
        string description,
        string color,
        Type valueType,
        PortDirection direction,
        PropertyControlBaseAttribute? controlAttribute,
        PropertyEditorAttribute2? editorAttribute,
        object? editorPropertyOwner,
        PropertyInfo? editorPropertyInfo,
        IEditorInfo? editorInfo,
        NodeGraph graph,
        Guid nodeId)
    {
        Name = name;
        Label = label;
        Description = description;
        Color = color;
        ValueType = valueType;
        Direction = direction;
        ControlAttribute = controlAttribute;
        EditorAttribute = controlAttribute == null ? editorAttribute : null;
        EditorPropertyOwner = EditorAttribute == null ? null : editorPropertyOwner;
        EditorPropertyInfo = EditorAttribute == null ? null : editorPropertyInfo;
        _editorInfo = editorInfo;
        _graph = graph;
        NodeId = nodeId;

        if (direction == PortDirection.Input)
        {
            var port = graph.Nodes[nodeId].Inputs[name];
            _currentValue = port.LocalValue;

            if (_currentValue == null && controlAttribute != null)
                _currentValue = controlAttribute.GetDefaultValue();
        }

        BeginEditCommand = new RelayCommand(() => _graph.BeginEdit());
        EndEditCommand = new RelayCommand(() => _graph.EndEdit());
        NotifyPreviewUpdateCommand = new RelayCommand(() => _graph.NotifyPreviewUpdate(NodeId));
    }

    public string Name { get; }
    public string Label { get; }
    public string Description { get; }
    public string Color { get; }
    public Type ValueType { get; }
    public PortDirection Direction { get; }

    public object? CurrentValue
    {
        get => _currentValue;
        set
        {
            if (!Equals(_currentValue, value))
            {
                _currentValue = value;
                OnPropertyChanged();

                if (Direction == PortDirection.Input)
                {
                    _graph.SetInputValue(NodeId, Name, value);
                }
            }
        }
    }

    public PropertyControlBaseAttribute? ControlAttribute { get; }

    /// <summary>
    ///     ポートとして固定に実装されているコントロール（NumberPort等）以外を使いたい場合の拡張ポイント。
    ///     プロパティに YMM4 標準の PropertyEditorAttribute2 継承属性（IPropertyEditorControl を実装した
    ///     コントロールを返すもの）が付与されている場合にセットされる。
    ///     ControlAttribute（このNode拡張独自の属性）が付いている場合はそちらを優先し、こちらは null のままになる。
    /// </summary>
    public PropertyEditorAttribute2? EditorAttribute { get; }

    /// <summary>
    ///     EditorAttribute を使う場合の、実際にこのプロパティを所有しているインスタンス
    ///     （ノード本体、またはネストしたコンテナのインスタンス）。
    ///     PropertyEditorAttribute2.SetBindings には合成した仮のラッパーではなく、必ずこの
    ///     「本物のインスタンス」と EditorPropertyInfo を渡す。多くのカスタムエディタ
    ///     （特に別ウィンドウを開いて編集する類のもの）は、渡された ItemProperty.PropertyOwner /
    ///     .Item に対して「編集中フラグを立てる」「他のプロパティも読む」「特定の型にキャストする」
    ///     といった、1つの値の読み書きだけに留まらない操作をすることがあるため、合成ラッパーでは
    ///     壊れてしまう。
    /// </summary>
    internal object? EditorPropertyOwner { get; }

    /// <summary>EditorPropertyOwner 上で、実際にこのポートに対応するプロパティを指す PropertyInfo。</summary>
    internal PropertyInfo? EditorPropertyInfo { get; }

    /// <summary>
    ///     EditorAttribute が示すコントロールが IPropertyEditorControl2 を実装している場合に渡す
    ///     IEditorInfo。OpenNodeEditorButton.SetEditorInfo で受け取った値が、
    ///     NodeEditorViewModel → TabViewModel → GraphViewModel → NodeViewModel を経由してここまで伝播する。
    ///     Node Editor パネルを直接開いた場合など、対応するアイテムが分からない状況では null のままになる。
    /// </summary>
    public IEditorInfo? EditorInfo
    {
        get => _editorInfo;
        internal set
        {
            if (!Equals(_editorInfo, value))
            {
                _editorInfo = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasControl => (ControlAttribute != null || EditorAttribute != null) && Direction == PortDirection.Input;

    public ICommand BeginEditCommand { get; }
    public ICommand EndEditCommand { get; }

    public ICommand NotifyPreviewUpdateCommand { get; }

    public bool IsConnected
    {
        get;
        set => SetField(ref field, value);
    }

    public Point Position
    {
        get;
        set => SetField(ref field, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateValueFromGraph(object? value)
    {
        if (!Equals(_currentValue, value))
        {
            _currentValue = value;
            OnPropertyChanged(nameof(CurrentValue));
        }
    }

    internal void ApplyDefaultToGraph()
    {
        if (Direction != PortDirection.Input) return;
        var port = _graph.Nodes[NodeId].Inputs[Name];
        if (port.LocalValue != null) return;
        if (ControlAttribute == null) return;
        var defaultValue = ControlAttribute.GetDefaultValue();
        if (defaultValue == null) return;
        _graph.SetInputValue(NodeId, Name, defaultValue);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public enum PortDirection
{
    Input,
    Output
}