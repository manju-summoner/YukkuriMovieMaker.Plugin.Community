using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private readonly float _def;
    private int _dig;

    private bool _isClicking;
    private bool _isDragging;
    private bool _isEditing;
    private float _max;
    private float _min;
    private Point _startPoint;
    private string _text = "";

    private float _value;

    public NumberPort(float def, float value, float min, float max, int dig, string unit)
    {
        InitializeComponent();

        _def = def;
        _value = value;
        _min = min;
        _max = max;
        _dig = dig;
        Text = Math.Round(_value, _dig).ToString("F" + _dig);
        Unit = unit;

        DataContext = this;
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
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged();
        }
    }

    public string Unit
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public object? Value
    {
        get => _value;
        set => Update((float?)value ?? _def);
    }

    public event PropertyChangedEventHandler? PropertyChanged;


    public void UpdateValueSilently(object? value)
    {
        try
        {
            var floatValue = (float?)value ?? _def;
            var v = floatValue;
            if (!float.IsNaN(_min) && floatValue < _min) v = _min;
            if (!float.IsNaN(_max) && floatValue > _max) v = _max;
            _value = (float)Math.Round(v, _dig);

            _text = _value.ToString("F" + _dig);

            OnPropertyChanged(nameof(Text));
        }
        catch
        {
            Value = value;
        }
    }

    public void ChangeSetting(float? min, float? max, int? digits, string? unit)
    {
        if (min != null)
            _min = (float)min;
        if (max != null)
            _max = (float)max;
        if (digits != null)
        {
            _dig = (int)digits;
            Text = Math.Round(_value, _dig).ToString("F" + _dig);
        }

        if (unit != null)
            Unit = unit;
    }

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void Update()
    {
        float v;
        if (Text == "")
            v = _def;
        else
            try
            {
                v = float.Parse(Text);
            }
            catch (Exception)
            {
                v = _value;
            }

        Update(v);
    }

    private void Update(float value)
    {
        var v = value;
        if (!float.IsNaN(_min) && value < _min) v = _min;
        if (!float.IsNaN(_max) && value > _max) v = _max;
        var newValue = (float)Math.Round(v, _dig);
        if (Math.Abs(_value - newValue) > 1e-8)
        {
            _value = newValue;
            _text = _value.ToString("F" + _dig);
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Value));
        }
        else
        {
            _text = _value.ToString("F" + _dig);
            OnPropertyChanged(nameof(Text));
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
                Update(_value + (float)delta * sensitivity);
                SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            box.ReleaseMouseCapture();
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
            SetCursorPos((int)_startPoint.X, (int)_startPoint.Y);
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
    }

    [GeneratedRegex("[^0-9.-]+")]
    private static partial Regex NumberRegex();
}