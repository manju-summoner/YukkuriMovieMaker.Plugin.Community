using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier;

public partial class BezierPort
{
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

        _isApplyingValue = true;

        try
        {
            ViewModel = new BezierEditorViewModel(BezierParser.Deserialize(Value));
            _textBoxBuffer = Value;

            SelectedPresetItem = FindMatchingPreset(Value);
        }
        finally
        {
            _isApplyingValue = false;
        }

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

    public BezierEasingPresetBase? SelectedPresetItem
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<BezierEasingPresetBase> Presets => BezierEasingPresets.All;

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
        BezierEasingPresets.All[0].Apply(curve);
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

            control.SelectedPresetItem = control.FindMatchingPreset(newValue);
        }
        finally
        {
            control._isApplyingValue = false;
        }
    }

    private BezierEasingPresetBase? FindMatchingPreset(string serializedValue)
    {
        try
        {
            BezierParser.Deserialize(serializedValue);

            foreach (var preset in Presets)
            {
                var testCurve = new BezierCurve();
                preset.Apply(testCurve);
                var testSerialized = BezierSerializer.Serialize(testCurve);

                if (testSerialized == serializedValue)
                    return preset;
            }
        }
        catch
        {
            // ignore
        }

        return null;
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

            SelectedPresetItem = FindMatchingPreset(serialized);
        }
        finally
        {
            _isWritingBackValue = false;
        }

        EndEditCommand?.Execute(null);
    }

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
            if (!BezierParser.TryDeserializeStrict(_textBoxBuffer, out curve))
            {
                TextBoxBuffer = Value;
                EndEditCommand?.Execute(null);
                return;
            }
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

    internal void OnPresetSelected(BezierEasingPresetBase? preset)
    {
        if (preset == null)
            return;

        if (_isApplyingValue)
            return;

        preset.Apply(ViewModel.Curve);

        Editor.InvalidateVisual();
        Editor.StartPreview();

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
        OnPresetSelected(sender is ComboBox { SelectedItem: BezierEasingPresetBase preset } ? preset : null);
    }
}