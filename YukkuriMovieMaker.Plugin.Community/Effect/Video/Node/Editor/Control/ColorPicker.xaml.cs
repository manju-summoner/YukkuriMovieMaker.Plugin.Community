using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class ColorPicker
{
    // Using a DependencyProperty as the backing store for SelectedColor.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    // Individual color component properties
    public static readonly DependencyProperty RProperty =
        DependencyProperty.Register(nameof(R), typeof(byte), typeof(ColorPicker),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbComponentChanged));

    public static readonly DependencyProperty GProperty =
        DependencyProperty.Register(nameof(G), typeof(byte), typeof(ColorPicker),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbComponentChanged));

    public static readonly DependencyProperty BProperty =
        DependencyProperty.Register(nameof(B), typeof(byte), typeof(ColorPicker),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbComponentChanged));

    public static readonly DependencyProperty AProperty =
        DependencyProperty.Register(nameof(A), typeof(byte), typeof(ColorPicker),
            new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnAlphaChanged));

    public static readonly DependencyProperty HProperty =
        DependencyProperty.Register(nameof(H), typeof(double), typeof(ColorPicker),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHsvComponentChanged, CoerceHue));

    public static readonly DependencyProperty SProperty =
        DependencyProperty.Register(nameof(S), typeof(double), typeof(ColorPicker),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHsvComponentChanged, CoerceSaturation));

    public static readonly DependencyProperty VProperty =
        DependencyProperty.Register(nameof(V), typeof(double), typeof(ColorPicker),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHsvComponentChanged, CoerceValue));

    private Point _a, _b, _c;

    // Triangle geometry
    private Point _baseA, _baseB, _baseC;

    // HSV values
    private double _currentHue;
    private double _currentSaturation = 1.0;
    private double _currentValue = 1.0;
    private bool _draggingInRing;
    private bool _draggingInTriangle;

    private bool _isDragging;

    private bool _suppressPropertyCallbacks;

    public ColorPicker()
    {
        InitializeComponent();
        Loaded += ColorPicker_Loaded;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public byte R
    {
        get => (byte)GetValue(RProperty);
        set => SetValue(RProperty, value);
    }

    public byte G
    {
        get => (byte)GetValue(GProperty);
        set => SetValue(GProperty, value);
    }

    public byte B
    {
        get => (byte)GetValue(BProperty);
        set => SetValue(BProperty, value);
    }

    public byte A
    {
        get => (byte)GetValue(AProperty);
        set => SetValue(AProperty, value);
    }

    public double H
    {
        get => (double)GetValue(HProperty);
        set => SetValue(HProperty, value);
    }

    public double S
    {
        get => (double)GetValue(SProperty);
        set => SetValue(SProperty, value);
    }

    public double V
    {
        get => (double)GetValue(VProperty);
        set => SetValue(VProperty, value);
    }

    private void ColorPicker_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeBaseTriangleGeometry();

        HsvFromColor(SelectedColor, out var h, out var s, out var v);
        _currentHue = h;
        _currentSaturation = s;
        _currentValue = v;

        UpdateRotatedTriangleGeometry();
        UpdateTriangleGradient();
        UpdateHueRingBrush();
        UpdateHueIndicator();

        _suppressPropertyCallbacks = true;
        UpdateAllProperties();
        _suppressPropertyCallbacks = false;

        UpdateIndicatorPosition();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker { _suppressPropertyCallbacks: false } cp)
            cp.OnSelectedColorChanged((Color)e.NewValue);
    }

    private void OnSelectedColorChanged(Color newColor)
    {
        HsvFromColor(newColor, out var h, out var s, out var v);
        if (s != 0)
            _currentHue = h;
        _currentSaturation = s;
        _currentValue = v;

        UpdateRotatedTriangleGeometry();
        UpdateTriangleGradient();
        UpdateHueIndicator();
        UpdateIndicatorPosition();

        _suppressPropertyCallbacks = true;
        UpdateAllProperties();
        _suppressPropertyCallbacks = false;
    }

    private static void OnRgbComponentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker { _suppressPropertyCallbacks: false } cp)
            cp.OnRgbComponentChanged();
    }

    private void OnRgbComponentChanged()
    {
        _suppressPropertyCallbacks = true;
        var newColor = Color.FromArgb(A, R, G, B);
        SelectedColor = newColor;

        HsvFromColor(newColor, out var h, out var s, out var v);
        if (s != 0)
            _currentHue = h;
        _currentSaturation = s;
        _currentValue = v;

        H = _currentHue;
        S = _currentSaturation;
        V = _currentValue;

        UpdateRotatedTriangleGeometry();
        UpdateTriangleGradient();
        UpdateHueIndicator();
        UpdateIndicatorPosition();
        _suppressPropertyCallbacks = false;
    }

    private static void OnAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker { _suppressPropertyCallbacks: false } cp)
            cp.OnAlphaChanged();
    }

    private void OnAlphaChanged()
    {
        _suppressPropertyCallbacks = true;
        SelectedColor = Color.FromArgb(A, R, G, B);
        _suppressPropertyCallbacks = false;
    }

    private static void OnHsvComponentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker { _suppressPropertyCallbacks: false } cp)
            cp.OnHsvComponentChanged();
    }

    private void OnHsvComponentChanged()
    {
        _currentHue = H;
        _currentSaturation = S;
        _currentValue = V;

        UpdateRotatedTriangleGeometry();
        UpdateTriangleGradient();
        UpdateHueIndicator();
        UpdateIndicatorPosition();

        _suppressPropertyCallbacks = true;
        var newColor = ColorFromHsv(_currentHue, _currentSaturation, _currentValue);
        SelectedColor = Color.FromArgb(A, newColor.R, newColor.G, newColor.B);
        R = newColor.R;
        G = newColor.G;
        B = newColor.B;
        _suppressPropertyCallbacks = false;
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

    private void UpdateAllProperties()
    {
        var color = SelectedColor;
        R = color.R;
        G = color.G;
        B = color.B;
        A = color.A;
        H = _currentHue;
        S = _currentSaturation;
        V = _currentValue;
    }

    private void UpdateHueFromPoint(Point pos)
    {
        var vec = pos - new Point(125, 125);
        var angle = Math.Atan2(vec.Y, vec.X);
        var degrees = angle * 180.0 / Math.PI;
        if (degrees < 0) degrees += 360.0;
        _currentHue = degrees;
        UpdateHueIndicator();
        UpdateRotatedTriangleGeometry();
        UpdateTriangleGradient();

        _suppressPropertyCallbacks = true;
        var newColor = ColorFromHsv(_currentHue, _currentSaturation, _currentValue);
        SelectedColor = Color.FromArgb(SelectedColor.A, newColor.R, newColor.G, newColor.B);
        UpdateAllProperties();
        _suppressPropertyCallbacks = false;

        UpdateIndicatorPosition();
    }

    private void UpdateHueIndicator()
    {
        var center = new Point(125, 125);
        const double outerRadius = 105.0;
        const double extra = 30.0;
        var rad = _currentHue * Math.PI / 180.0;
        var direction = new Vector(Math.Cos(rad), Math.Sin(rad));
        var startPoint = center + direction * outerRadius;
        var endPoint = center + direction * (outerRadius + extra);
        HueIndicator.X1 = startPoint.X;
        HueIndicator.Y1 = startPoint.Y;
        HueIndicator.X2 = endPoint.X;
        HueIndicator.Y2 = endPoint.Y;
    }


    #region Color conversion and internal calculations

    // Convert between RGB and HSV
    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var hi = (int)Math.Floor(hue / 60) % 6;
        var f = hue / 60 - Math.Floor(hue / 60);
        var v = (byte)(value * 255);
        var p = (byte)(v * (1 - saturation));
        var q = (byte)(v * (1 - f * saturation));
        var t = (byte)(v * (1 - (1 - f) * saturation));
        return hi switch
        {
            0 => Color.FromArgb(255, v, t, p),
            1 => Color.FromArgb(255, q, v, p),
            2 => Color.FromArgb(255, p, v, t),
            3 => Color.FromArgb(255, p, q, v),
            4 => Color.FromArgb(255, t, p, v),
            5 => Color.FromArgb(255, v, p, q),
            _ => Colors.White
        };
    }

    // Convert color to hsv
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
        else if (Math.Abs(max - b) < 0.001) hue = 60 * ((r - g) / delta + 4);

        if (hue < 0) hue += 360;
    }

    #endregion

    #region Triangle geometry gradient

    // Initialize the base triangle geometry
    private void InitializeBaseTriangleGeometry()
    {
        var center = new Point(125, 125);
        const double r = 105;
        _baseB = center with { X = center.X + r };
        _baseA = new Point(center.X - r / 2, center.Y - r * Math.Sqrt(3) / 2);
        _baseC = new Point(center.X - r / 2, center.Y + r * Math.Sqrt(3) / 2);
    }

    // Update the rotated triangle geometry
    private void UpdateRotatedTriangleGeometry()
    {
        var center = new Point(125, 125);
        var rad = _currentHue * Math.PI / 180;
        _a = RotatePoint(_baseA, center, rad);
        _b = RotatePoint(_baseB, center, rad);
        _c = RotatePoint(_baseC, center, rad);
        SvTriangle.Points = [_a, _b, _c];
    }

    // Rotate a point around a center
    private static Point RotatePoint(Point pt, Point center, double angleRadians)
    {
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        var v = pt - center;
        var x = v.X * cos - v.Y * sin;
        var y = v.X * sin + v.Y * cos;
        return new Point(center.X + x, center.Y + y);
    }

    // Update the triangle gradient
    private void UpdateTriangleGradient()
    {
        // Compute the bounding box of the triangle
        var minX = Math.Min(_a.X, Math.Min(_b.X, _c.X));
        var minY = Math.Min(_a.Y, Math.Min(_b.Y, _c.Y));
        var maxX = Math.Max(_a.X, Math.Max(_b.X, _c.X));
        var maxY = Math.Max(_a.Y, Math.Max(_b.Y, _c.Y));
        var bmpWidth = (int)Math.Ceiling(maxX - minX);
        var bmpHeight = (int)Math.Ceiling(maxY - minY);
        if (bmpWidth <= 0 || bmpHeight <= 0)
        {
            Dispatcher.BeginInvoke(new Action(UpdateTriangleGradient),
                DispatcherPriority.Render);
            return;
        }

        // Create a bitmap and fill it with the gradient
        var bmp = new WriteableBitmap(bmpWidth, bmpHeight, 96, 96, PixelFormats.Bgra32, null);
        var stride = bmpWidth * 4;
        var pixels = new byte[bmpHeight * stride];
        var pureHue = ColorFromHsv(_currentHue, 1, 1);

        // Fill the bitmap with the gradient
        for (var y = 0; y < bmpHeight; y++)
        for (var x = 0; x < bmpWidth; x++)
        {
            var p = new Point(minX + x, minY + y);
            ComputeBarycentricCoordinates(p, _a, _b, _c, out var wA, out var wB, out var wC);
            var index = y * stride + x * 4;
            if (wA >= 0 && wB >= 0 && wC >= 0)
            {
                var r = (byte)Math.Clamp(wA * 255 + wB * pureHue.R, 0, 255);
                var g = (byte)Math.Clamp(wA * 255 + wB * pureHue.G, 0, 255);
                var b = (byte)Math.Clamp(wA * 255 + wB * pureHue.B, 0, 255);
                pixels[index + 0] = b;
                pixels[index + 1] = g;
                pixels[index + 2] = r;
                pixels[index + 3] = 255;
            }
            else
            {
                pixels[index + 0] = 0;
                pixels[index + 1] = 0;
                pixels[index + 2] = 0;
                pixels[index + 3] = 0;
            }
        }

        bmp.WritePixels(new Int32Rect(0, 0, bmpWidth, bmpHeight), pixels, stride, 0);

        var brush = new ImageBrush(bmp)
        {
            Viewbox = new Rect(0, 0, bmpWidth, bmpHeight),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(minX, minY, bmpWidth, bmpHeight),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = System.Windows.Media.Stretch.Fill
        };
        SvTriangle.Fill = brush;
    }

    // Update the indicator position
    private void UpdateIndicatorPosition()
    {
        var wA = _currentValue * (1 - _currentSaturation);
        var wB = _currentValue * _currentSaturation;
        var wC = 1 - _currentValue;
        var pos = new Point(
            wA * _a.X + wB * _b.X + wC * _c.X,
            wA * _a.Y + wB * _b.Y + wC * _c.Y);
        Canvas.SetLeft(SelectorIndicator, pos.X - SelectorIndicator.Width / 2);
        Canvas.SetTop(SelectorIndicator, pos.Y - SelectorIndicator.Height / 2);
        SelectorIndicator.Visibility = Visibility.Visible;
    }

    // Compute barycentric coordinates
    private static void ComputeBarycentricCoordinates(Point p, Point a, Point b, Point c, out double wA,
        out double wB, out double wC)
    {
        var det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        wA = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
        wB = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
        wC = 1 - wA - wB;
    }

    #endregion

    #region Hue circle generation

    // Generate the hue ring bitmap
    private static WriteableBitmap GenerateHueRingBitmap()
    {
        const int width = 250;
        const int height = 250;
        var bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        const int stride = width * 4;
        var pixels = new byte[height * stride];
        var center = new Point(125, 125);
        const double innerRadius = 105.0;
        const double outerRadius = 125.0;
        // Fill the bitmap with the hue ring
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var p = new Point(x, y);
            var v = p - center;
            var d = v.Length;
            var index = y * stride + x * 4;
            if (d is >= innerRadius and <= outerRadius)
            {
                var angle = Math.Atan2(v.Y, v.X);
                var degrees = angle * 180 / Math.PI;
                if (degrees < 0) degrees += 360;
                var c = ColorFromHsv(degrees, 1, 1);
                pixels[index + 0] = c.B;
                pixels[index + 1] = c.G;
                pixels[index + 2] = c.R;
                pixels[index + 3] = 255;
            }
            else
            {
                pixels[index + 0] = 0;
                pixels[index + 1] = 0;
                pixels[index + 2] = 0;
                pixels[index + 3] = 0;
            }
        }

        bmp.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bmp;
    }

    private void UpdateHueRingBrush()
    {
        var bmp = GenerateHueRingBitmap();
        var brush = new ImageBrush(bmp)
        {
            Stretch = System.Windows.Media.Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        HueRing.Stroke = brush;
    }

    #endregion

    #region Mouse operation

    private void ColorWheelCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ColorWheelCanvas);
        var vec = pos - new Point(125, 125);
        var dist = vec.Length;
        // Check if the mouse is in the ring
        if (dist is >= 105 and <= 125)
        {
            _draggingInRing = true;
            _isDragging = true;
            ColorWheelCanvas.CaptureMouse();
            UpdateHueFromPoint(pos);
        }
        else if (PointInTriangle(pos, _a, _b, _c))
        {
            _draggingInTriangle = true;
            _isDragging = true;
            ColorWheelCanvas.CaptureMouse();
            UpdateSvFromPoint(pos);
        }

        e.Handled = true;
    }

    private void ColorWheelCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;
        var pos = e.GetPosition(ColorWheelCanvas);
        if (_draggingInRing)
            UpdateHueFromPoint(pos);
        else if (_draggingInTriangle)
            UpdateSvFromPoint(pos);
        e.Handled = true;
    }

    private void ColorWheelCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _draggingInRing = false;
        _draggingInTriangle = false;
        ColorWheelCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    #endregion

    #region Calcuration for SV operation

    // Clamp a point to a triangle
    private Point ClampPointToTriangle(Point p, Point a, Point b, Point c)
    {
        if (PointInTriangle(p, a, b, c))
            return p;
        var cpAb = ClosestPointOnLineSegment(a, b, p);
        var cpBc = ClosestPointOnLineSegment(b, c, p);
        var cpCa = ClosestPointOnLineSegment(c, a, p);
        var dAb = (p - cpAb).Length;
        var dBc = (p - cpBc).Length;
        var dCa = (p - cpCa).Length;
        var closest = cpAb;
        var dMin = dAb;
        if (dBc < dMin)
        {
            dMin = dBc;
            closest = cpBc;
        }

        if (!(dCa < dMin)) return closest;
        closest = cpCa;
        return closest;
    }

    // Compute the closest point on a line segment
    private static Point ClosestPointOnLineSegment(Point a, Point b, Point p)
    {
        var ab = b - a;
        var t = Vector.Multiply(p - a, ab) / ab.LengthSquared;
        t = Math.Clamp(t, 0, 1);
        return a + ab * t;
    }

    // Update the SV values from a point
    private void UpdateSvFromPoint(Point pos)
    {
        pos = ClampPointToTriangle(pos, _a, _b, _c);
        ComputeBarycentricCoordinates(pos, _a, _b, _c, out var wA, out var wB, out _);
        var sum = wA + wB;
        _currentValue = sum;
        _currentSaturation = sum > 0 ? wB / sum : 0;

        _suppressPropertyCallbacks = true;
        var newColor = ColorFromHsv(_currentHue, _currentSaturation, _currentValue);
        SelectedColor = Color.FromArgb(SelectedColor.A, newColor.R, newColor.G, newColor.B);
        UpdateAllProperties();
        _suppressPropertyCallbacks = false;

        Canvas.SetLeft(SelectorIndicator, pos.X - SelectorIndicator.Width / 2);
        Canvas.SetTop(SelectorIndicator, pos.Y - SelectorIndicator.Height / 2);
        SelectorIndicator.Visibility = Visibility.Visible;
    }

    private static bool PointInTriangle(Point p, Point a, Point b, Point c)
    {
        ComputeBarycentricCoordinates(p, a, b, c, out var wA, out var wB, out var wC);
        return wA >= 0 && wB >= 0 && wC >= 0;
    }

    #endregion
}