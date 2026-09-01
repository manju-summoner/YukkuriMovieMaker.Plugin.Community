using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

/// <summary>
///     Interaction logic for NumberPort.xaml
/// </summary>
public sealed partial class NumberPort
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(float),
            typeof(NumberPort),
            new FrameworkPropertyMetadata(
                0.0f,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register(
            nameof(Min),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(
            nameof(Max),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty DigitsProperty =
        DependencyProperty.Register(
            nameof(Digits),
            typeof(int),
            typeof(NumberPort),
            new PropertyMetadata(2, OnConfigChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(string),
            typeof(NumberPort),
            new PropertyMetadata("", OnUnitChanged));

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(
            nameof(Default),
            typeof(float),
            typeof(NumberPort),
            new PropertyMetadata(0f));

    private bool _isClicking;
    private bool _isDragging;
    private bool _isEditing;
    private Point _startPoint;

    public NumberPort()
    {
        InitializeComponent();
        Update();
    }

    public bool IsFocusable
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public string Text
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = "";

    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float Min
    {
        get => (float)GetValue(MinProperty);
        init => SetValue(MinProperty, value);
    }

    public float Max
    {
        get => (float)GetValue(MaxProperty);
        init => SetValue(MaxProperty, value);
    }

    public int Digits
    {
        get => (int)GetValue(DigitsProperty);
        init => SetValue(DigitsProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        init => SetValue(UnitProperty, value);
    }

    public float Default
    {
        get => (float)GetValue(DefaultProperty);
        init
        {
            SetValue(DefaultProperty, value);
            SetValue(ValueProperty, value);
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        if (!control._isEditing)
            control.Text = ((float)e.NewValue).ToString("F" + control.Digits, CultureInfo.InvariantCulture);
    }

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        control.Text = control.Value.ToString("F" + control.Digits, CultureInfo.InvariantCulture);
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumberPort)d;
        control.OnPropertyChanged(nameof(Unit));
    }

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private void Update()
    {
        float v;
        if (Text == "")
            v = Default;
        else
            try
            {
                v = float.Parse(Text, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                v = Value;
            }

        Update(v);
    }

    private void Update(float value)
    {
        var v = value;
        if (!float.IsNaN(Min) && value < Min) v = Min;
        if (!float.IsNaN(Max) && value > Max) v = Max;
        var newValue = (float)Math.Round(v, Digits);
        if (Math.Abs(Value - newValue) > 1e-8)
        {
            Value = newValue;
        }
        else
        {
            Text = newValue.ToString("F" + Digits, CultureInfo.InvariantCulture);
        }

        Keyboard.ClearFocus();
        IsFocusable = false;
        if (!_isDragging)
            Mouse.OverrideCursor = null;
    }

    private void Box_PreviewMouseDown(object _, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Box_PreviewMouseLeftButtonDown(object o, MouseButtonEventArgs e)
    {
        if (o is not TextBox box) return;
        _isClicking = true;
        if (_isEditing) return;
        _startPoint = box.PointToScreen(e.GetPosition(box));
        e.Handled = true;
        IsFocusable = false;
        Mouse.OverrideCursor = Cursors.None;
        BeginEditCommand?.Execute(null);
        box.CaptureMouse();
    }

    private void Box_PreviewMouseMove(object o, MouseEventArgs e)
    {
        if (o is not TextBox box) return;
        try
        {
            if (!_isClicking || _isEditing) return;
            var currentPoint = box.PointToScreen(e.GetPosition(box));
            var delta = currentPoint.X - _startPoint.X;

            if (Math.Abs(delta) > SystemParameters.MinimumHorizontalDragDistance || _isDragging)
            {
                _isDragging = true;

                const float sensitivity = 0.01f;
                Update(Value + (float)delta * sensitivity);
                SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            box.ReleaseMouseCapture();
            EndEditCommand?.Execute(null);
            Console.Error.WriteLine(ex);
        }
    }

    private void Box_PreviewMouseLeftButtonUp(object o, MouseButtonEventArgs e)
    {
        if (o is not TextBox box) return;
        _isClicking = false;
        Mouse.OverrideCursor = null;
        box.ReleaseMouseCapture();

        if (_isDragging)
        {
            _isDragging = false;
            Update(Value);
            SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
            EndEditCommand?.Execute(null);
            e.Handled = true;
        }
        else
        {
            _isEditing = true;
            IsFocusable = true;
            Keyboard.Focus(box);
        }
    }

    private void Box_LostFocus(object _, RoutedEventArgs e)
    {
        _isEditing = false;
        Update();
        EndEditCommand?.Execute(null);
    }

    private new void PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = NumberRegex().IsMatch(e.Text);
    }

    private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string?)e.DataObject.GetData(typeof(string)) ?? string.Empty;
            if (NumberRegex().IsMatch(text)) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void ValueSubmit(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Return) return;
        Update();
        _isEditing = false;
        EndEditCommand?.Execute(null);
    }

    [GeneratedRegex("[^0-9.-]+")]
    private static partial Regex NumberRegex();
}