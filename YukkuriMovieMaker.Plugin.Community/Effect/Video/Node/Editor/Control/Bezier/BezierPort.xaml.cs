using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier;

public partial class BezierPort
{
    /// <summary>
    ///     初期状態(直線)を表すシリアライズ済み文字列。
    ///     ValueProperty のデフォルト値、および BezierPortControlAttribute.GetDefaultValue の
    ///     既定値として共通で用いる。
    /// </summary>
    public static readonly string LinearDefault = CreateLinearDefault();

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(BezierPort),
            new FrameworkPropertyMetadata(
                LinearDefault,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(
            nameof(Default),
            typeof(string),
            typeof(BezierPort),
            new PropertyMetadata(""));

    private bool _isApplyingValue;

    private bool _isWritingBackValue;

    private string _textBoxBuffer;

    public BezierPort()
    {
        InitializeComponent();

        ViewModel = new BezierEditorViewModel(BezierParser.Deserialize(Value));
        _textBoxBuffer = Value;
        SelectedPresetItem = Presets[0];

        Editor.CurveChanged += OnEditorCurveChanged;
        Editor.EditCompleted += OnEditorEditCompleted;
    }

    public string TextBoxBuffer
    {
        get => _textBoxBuffer;
        set
        {
            _textBoxBuffer = value;
            OnPropertyChanged();
        }
    }

    public object SelectedPresetItem
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BezierEasingPreset> Presets => BezierEasingPresets.All;

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Default
    {
        get => (string)GetValue(DefaultProperty);
        init
        {
            SetValue(DefaultProperty, value);
            SetValue(ValueProperty, value);
        }
    }

    public BezierEditorViewModel ViewModel
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    private static string CreateLinearDefault()
    {
        var curve = new BezierCurve();
        BezierEasingPresets.Apply(curve, BezierEasingPresets.All[0]);
        return BezierSerializer.Serialize(curve);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BezierPort)d;

        if (control._isWritingBackValue)
            return;

        control._isApplyingValue = true;

        try
        {
            var newValue = (string)e.NewValue;
            var curve = BezierParser.Deserialize(newValue);

            control.ViewModel = new BezierEditorViewModel(curve);
            control.TextBoxBuffer = newValue;
        }
        finally
        {
            control._isApplyingValue = false;
        }
    }

    private void OnEditorCurveChanged(object? sender, EventArgs e)
    {
    }

    private void OnEditorEditCompleted(object? sender, EventArgs e)
    {
        if (_isApplyingValue)
            return;

        var serialized = BezierSerializer.Serialize(ViewModel.Curve);

        if (serialized == Value)
            return;

        BeginEditCommand?.Execute(null);

        _isWritingBackValue = true;

        try
        {
            Value = serialized;
            TextBoxBuffer = serialized;
        }
        finally
        {
            _isWritingBackValue = false;
        }

        EndEditCommand?.Execute(null);
    }

    /// <summary>
    ///     テキストボックスの確定操作(LostFocus/Enter)から呼ばれる。
    ///     編集中の1文字ごとにグラフへ通知することを避けるため、確定時にのみ Value へ反映する。
    /// </summary>
    internal void OnTextBoxCommit()
    {
        if (_isApplyingValue)
            return;

        if (_textBoxBuffer == Value)
        {
            EndEditCommand?.Execute(null);
            return;
        }

        BezierCurve curve;

        try
        {
            curve = BezierParser.Deserialize(_textBoxBuffer);
        }
        catch
        {
            TextBoxBuffer = Value;
            EndEditCommand?.Execute(null);
            return;
        }

        BeginEditCommand?.Execute(null);

        _isWritingBackValue = true;

        try
        {
            ViewModel = new BezierEditorViewModel(curve);
            var normalized = BezierSerializer.Serialize(curve);
            Value = normalized;
            TextBoxBuffer = normalized;
        }
        finally
        {
            _isWritingBackValue = false;
        }

        EndEditCommand?.Execute(null);
    }

    /// <summary>
    ///     コンボボックスでプリセットが選択された時に呼ばれる。
    ///     プリセットの適用は単一の確定操作として即座にコミットする。
    /// </summary>
    internal void OnPresetSelected(BezierEasingPreset? preset)
    {
        if (preset == null)
            return;

        if (_isApplyingValue)
            return;

        BezierEasingPresets.Apply(ViewModel.Curve, preset);

        Editor.InvalidateVisual();

        var serialized = BezierSerializer.Serialize(ViewModel.Curve);

        if (serialized == Value)
            return;

        BeginEditCommand?.Execute(null);

        _isWritingBackValue = true;

        try
        {
            Value = serialized;
            TextBoxBuffer = serialized;
        }
        finally
        {
            _isWritingBackValue = false;
        }

        EndEditCommand?.Execute(null);
    }

    internal void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
    {
        BeginEditCommand?.Execute(null);
    }

    internal void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        OnTextBoxCommit();
    }

    internal void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
            OnTextBoxCommit();
    }

    internal void OnPresetComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPresetSelected((sender as ComboBox)?.SelectedItem as BezierEasingPreset);
    }
}