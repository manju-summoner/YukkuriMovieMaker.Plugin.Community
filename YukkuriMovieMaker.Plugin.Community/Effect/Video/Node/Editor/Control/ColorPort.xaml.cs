using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class ColorPort
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPort),
            new FrameworkPropertyMetadata(
                Colors.White,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public static readonly DependencyProperty DefaultColorProperty =
        DependencyProperty.Register(
            nameof(DefaultColor),
            typeof(Color),
            typeof(ColorPort),
            new PropertyMetadata(Colors.White));

    public static readonly DependencyProperty RedProperty =
        DependencyProperty.Register(nameof(Red), typeof(byte), typeof(ColorPort),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged));

    public static readonly DependencyProperty GreenProperty =
        DependencyProperty.Register(nameof(Green), typeof(byte), typeof(ColorPort),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged));

    public static readonly DependencyProperty BlueProperty =
        DependencyProperty.Register(nameof(Blue), typeof(byte), typeof(ColorPort),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged));

    public static readonly DependencyProperty AlphaProperty =
        DependencyProperty.Register(nameof(Alpha), typeof(byte), typeof(ColorPort),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged));

    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(nameof(Hue), typeof(double), typeof(ColorPort),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged, CoerceHue));

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(nameof(Saturation), typeof(double), typeof(ColorPort),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged, CoerceSaturation));

    public static readonly DependencyProperty BValueProperty =
        DependencyProperty.Register(nameof(BValue), typeof(double), typeof(ColorPort),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnComponentChanged, CoerceValue));

    private bool _suppress;

    public ColorPort()
    {
        InitializeComponent();

        Popup.Opened += Popup_Opened;
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color DefaultColor
    {
        get => (Color)GetValue(DefaultColorProperty);
        init
        {
            SetValue(DefaultColorProperty, value);
            SetValue(SelectedColorProperty, value);
        }
    }

    public byte Red
    {
        get => (byte)GetValue(RedProperty);
        set => SetValue(RedProperty, value);
    }

    public byte Green
    {
        get => (byte)GetValue(GreenProperty);
        set => SetValue(GreenProperty, value);
    }

    public byte Blue
    {
        get => (byte)GetValue(BlueProperty);
        set => SetValue(BlueProperty, value);
    }

    public byte Alpha
    {
        get => (byte)GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double BValue
    {
        get => (double)GetValue(BValueProperty);
        set => SetValue(BValueProperty, value);
    }

    private static object CoerceHue(DependencyObject d, object value)
    {
        var hue = (double)value;
        if (hue < 0) return 0.0;
        if (hue >= 360) return 359.999;
        return hue;
    }

    private static object CoerceSaturation(DependencyObject d, object value)
    {
        var sat = (double)value;
        return Math.Clamp(sat, 0.0, 1.0);
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var val = (double)value;
        return Math.Clamp(val, 0.0, 1.0);
    }

    private static void OnComponentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cp = (ColorPort)d;
        if (cp._suppress)
            return;

        // Component changed, update SelectedColor
        cp._suppress = true;
        cp.SelectedColor = Color.FromArgb(cp.Alpha, cp.Red, cp.Green, cp.Blue);
        cp._suppress = false;
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cp = (ColorPort)d;
        if (cp._suppress)
            return;

        var newColor = (Color)e.NewValue;
        cp._suppress = true;
        cp.UpdateAllPropertiesFromColor(newColor);
        cp._suppress = false;
    }

    private void UpdateAllPropertiesFromColor(Color color)
    {
        // Update RGB and Alpha
        Red = color.R;
        Green = color.G;
        Blue = color.B;
        Alpha = color.A;

        // Update HSV
        HsvFromColor(color, out var h, out var s, out var v);
        Hue = h;
        Saturation = s;
        BValue = v;
    }

    private static void HsvFromColor(Color color, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        // Calculate value
        value = max;

        // Calculate saturation
        saturation = max == 0 ? 0 : delta / max;

        // Calculate hue
        hue = 0;
        if (delta == 0)
            hue = 0;
        else if (Math.Abs(max - r) < 0.001)
            hue = 60 * ((g - b) / delta % 6);
        else if (Math.Abs(max - g) < 0.001)
            hue = 60 * ((b - r) / delta + 2);
        else if (Math.Abs(max - b) < 0.001)
            hue = 60 * ((r - g) / delta + 4);

        if (hue < 0) hue += 360;
    }

    private void btnColor_Click(object sender, RoutedEventArgs e)
    {
        if (ColorPickerHost.Content == null)
        {
            var picker = new ColorPicker();
            picker.SetBinding(ColorPicker.RProperty,
                new Binding(nameof(Red)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.GProperty,
                new Binding(nameof(Green)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.BProperty,
                new Binding(nameof(Blue)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.AProperty,
                new Binding(nameof(Alpha)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.HProperty,
                new Binding(nameof(Hue)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.SProperty,
                new Binding(nameof(Saturation)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.VProperty,
                new Binding(nameof(BValue)) { Source = this, Mode = BindingMode.TwoWay });
            picker.SetBinding(ColorPicker.SelectedColorProperty,
                new Binding(nameof(SelectedColor)) { Source = this, Mode = BindingMode.TwoWay });
            ColorPickerHost.Content = picker;
        }

        Popup.IsOpen = true;
    }

    private void Popup_Opened(object? sender, EventArgs e)
    {
        var parentScale = GetCumulativeScale(this);

        if (!(parentScale > 0) || !(Math.Abs(parentScale - 1.0) > 0.001) ||
            Popup.Child is not FrameworkElement child) return;
        var inverseScale = 1.0 / parentScale;
        child.LayoutTransform = new ScaleTransform(inverseScale, inverseScale);
    }

    private static double GetCumulativeScale(DependencyObject element)
    {
        var scaleX = 1.0;

        var current = element;
        while (current != null)
        {
            switch (current)
            {
                case FrameworkElement { LayoutTransform: ScaleTransform scaleTransform }:
                    scaleX *= scaleTransform.ScaleX;
                    break;
                case FrameworkElement { LayoutTransform: TransformGroup transformGroup }:
                {
                    foreach (var transform in transformGroup.Children)
                        if (transform is ScaleTransform st)
                            scaleX *= st.ScaleX;

                    break;
                }
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return scaleX;
    }

    private void Popup_OnClosed(object? sender, EventArgs e)
    {
        BeginEditCommand?.Execute(null);
        if (DataContext is PortViewModel vm)
            vm.CurrentValue = SelectedColor;
        EndEditCommand?.Execute(null);
        OnPropertyChanged(nameof(SelectedColor));
    }

    private void UIElement_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}

public class ColorToHexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
            return $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";
        return "#ffffffff";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s)
            return DependencyProperty.UnsetValue;

        s = s.Trim();
        if (s.StartsWith('#'))
            s = s[1..];

        try
        {
            switch (s.Length)
            {
                case 8:
                {
                    var r = byte.Parse(s[..2], NumberStyles.HexNumber);
                    var g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber);
                    var a = byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(a, r, g, b);
                }
                case 6:
                {
                    var r = byte.Parse(s[..2], NumberStyles.HexNumber);
                    var g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber);
                    var b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(255, r, g, b);
                }
            }
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }

        return DependencyProperty.UnsetValue;
    }
}