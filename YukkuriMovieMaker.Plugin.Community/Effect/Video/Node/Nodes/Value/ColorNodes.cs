using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Value;

internal static class ColorConversionUtility
{
    private static double Clamp01(double v)
    {
        return Math.Clamp(v, 0, 1);
    }

    public static byte ToByte(double v01)
    {
        return (byte)Math.Round(Clamp01(v01) * 255, MidpointRounding.AwayFromZero);
    }

    public static (double H, double S, double V) RgbToHsv(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double h;
        if (delta < 1e-9) h = 0;
        else if (Math.Abs(max - r) < 1e-9) h = 60 * (((g - b) / delta % 6 + 6) % 6);
        else if (Math.Abs(max - g) < 1e-9) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);

        var s = max < 1e-9 ? 0 : delta / max;
        return (h, s, max);
    }

    public static (double R, double G, double B) HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r1, g1, b1) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return (r1 + m, g1 + m, b1 + m);
    }

    public static (double C, double M, double Y, double K) RgbToCmyk(double r, double g, double b)
    {
        var k = 1 - Math.Max(r, Math.Max(g, b));
        if (k >= 1 - 1e-9) return (0, 0, 0, 1);
        var c = (1 - r - k) / (1 - k);
        var m = (1 - g - k) / (1 - k);
        var y = (1 - b - k) / (1 - k);
        return (c, m, y, k);
    }

    public static (double R, double G, double B) CmykToRgb(double c, double m, double y, double k)
    {
        var r = (1 - c) * (1 - k);
        var g = (1 - m) * (1 - k);
        var b = (1 - y) * (1 - k);
        return (r, g, b);
    }

    public static (double Y, double Cb, double Cr) RgbToYCbCr(double r, double g, double b)
    {
        var r255 = r * 255;
        var g255 = g * 255;
        var b255 = b * 255;
        var y = 0.299 * r255 + 0.587 * g255 + 0.114 * b255;
        var cb = -0.168736 * r255 - 0.331264 * g255 + 0.5 * b255 + 128;
        var cr = 0.5 * r255 - 0.418688 * g255 - 0.081312 * b255 + 128;
        return (y, cb, cr);
    }

    public static (double R, double G, double B) YCbCrToRgb(double y, double cb, double cr)
    {
        var r = y + 1.402 * (cr - 128);
        var g = y - 0.344136 * (cb - 128) - 0.714136 * (cr - 128);
        var b = y + 1.772 * (cb - 128);
        return (r / 255.0, g / 255.0, b / 255.0);
    }

    private static double SrgbToLinear(double c)
    {
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double LinearToSrgb(double c)
    {
        return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
    }

    private static double LabF(double t)
    {
        return t > 216.0 / 24389.0 ? Math.Cbrt(t) : (24389.0 / 27.0 * t + 16) / 116.0;
    }

    private static double LabFInv(double t)
    {
        return t * t * t > 216.0 / 24389.0 ? t * t * t : (116.0 * t - 16) * 27.0 / 24389.0;
    }

    public static (double L, double A, double B) RgbToLab(double r, double g, double b)
    {
        var rl = SrgbToLinear(r);
        var gl = SrgbToLinear(g);
        var bl = SrgbToLinear(b);

        var x = 0.4124564 * rl + 0.3575761 * gl + 0.1804375 * bl;
        var y = 0.2126729 * rl + 0.7151522 * gl + 0.0721750 * bl;
        var z = 0.0193339 * rl + 0.1191920 * gl + 0.9503041 * bl;

        const double xn = 0.95047, yn = 1.0, zn = 1.08883;
        var fx = LabF(x / xn);
        var fy = LabF(y / yn);
        var fz = LabF(z / zn);

        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    public static (double R, double G, double B) LabToRgb(double l, double a, double b)
    {
        const double xn = 0.95047, yn = 1.0, zn = 1.08883;
        var fy = (l + 16) / 116;
        var fx = fy + a / 500;
        var fz = fy - b / 200;

        var x = xn * LabFInv(fx);
        var y = yn * LabFInv(fy);
        var z = zn * LabFInv(fz);

        var rl = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
        var gl = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
        var bl = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

        return (LinearToSrgb(Clamp01(rl)), LinearToSrgb(Clamp01(gl)), LinearToSrgb(Clamp01(bl)));
    }

    public static (double L, double C, double H) RgbToOklch(double r, double g, double b)
    {
        var rl = SrgbToLinear(r);
        var gl = SrgbToLinear(g);
        var bl = SrgbToLinear(b);

        var l = 0.4122214708 * rl + 0.5363325363 * gl + 0.0514459929 * bl;
        var m = 0.2119034982 * rl + 0.6806995451 * gl + 0.1073969566 * bl;
        var s = 0.0883024619 * rl + 0.2817188376 * gl + 0.6299787005 * bl;

        var l_ = Math.Cbrt(l);
        var m_ = Math.Cbrt(m);
        var s_ = Math.Cbrt(s);

        var lab = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
        var oa = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
        var ob = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;

        var c = Math.Sqrt(oa * oa + ob * ob);
        var h = Math.Atan2(ob, oa) * 180 / Math.PI;
        if (h < 0) h += 360;
        return (lab, c, h);
    }

    public static (double R, double G, double B) OklchToRgb(double l, double c, double h)
    {
        var hr = h * Math.PI / 180;
        var oa = c * Math.Cos(hr);
        var ob = c * Math.Sin(hr);

        var l_ = l + 0.3963377774 * oa + 0.2158037573 * ob;
        var m_ = l - 0.1055613458 * oa - 0.0638541728 * ob;
        var s_ = l - 0.0894841775 * oa - 1.2914855480 * ob;

        var ll = l_ * l_ * l_;
        var mm = m_ * m_ * m_;
        var ss = s_ * s_ * s_;

        var rl = 4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss;
        var gl = -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss;
        var bl = -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss;

        return (LinearToSrgb(Clamp01(rl)), LinearToSrgb(Clamp01(gl)), LinearToSrgb(Clamp01(bl)));
    }

    public static (double H, double W, double Bk) RgbToHwb(double r, double g, double b)
    {
        var (h, s, v) = RgbToHsv(r, g, b);
        return (h, (1 - s) * v, 1 - v);
    }

    public static (double R, double G, double B) HwbToRgb(double h, double w, double bk)
    {
        if (w + bk >= 1)
        {
            var gray = w / (w + bk);
            return (gray, gray, gray);
        }

        var v = 1 - bk;
        var s = v < 1e-9 ? 0 : 1 - w / v;
        return HsvToRgb(h, s, v);
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.RGBToColorNode), nameof(TextNode.RGBToColorNodeDescription),
    typeof(TextNode))]
public class RGBToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Red), nameof(TextNode.RedDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float R
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Green), nameof(TextNode.GreenDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float G
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Blue), nameof(TextNode.BlueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        Result = Color.FromRgb(ColorConversionUtility.ToByte(R / 255.0), ColorConversionUtility.ToByte(G / 255.0),
            ColorConversionUtility.ToByte(B / 255.0));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToRGBNode), nameof(TextNode.ColorToRGBNodeDescription),
    typeof(TextNode))]
public class ColorToRGBNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Red), nameof(TextNode.RedDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float R
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.Green), nameof(TextNode.GreenDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float G
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.Blue), nameof(TextNode.BlueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        R = Input.R;
        G = Input.G;
        B = Input.B;
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.HSVToColorNode), nameof(TextNode.HSVToColorNodeDescription),
    typeof(TextNode))]
public class HSVToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 360, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Saturation), nameof(TextNode.SaturationDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Default = 100f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float S
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Brightness), nameof(TextNode.BrightnessDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1, Default = 100f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float V
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.HsvToRgb(H, S / 100.0, V / 100.0);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToHSVNode), nameof(TextNode.ColorToHSVNodeDescription),
    typeof(TextNode))]
public class ColorToHSVNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.Saturation), nameof(TextNode.SaturationDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float S
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.Brightness), nameof(TextNode.BrightnessDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float V
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (h, s, v) = ColorConversionUtility.RgbToHsv(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        H = (float)h;
        S = (float)(s * 100);
        V = (float)(v * 100);
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.CMYKToColorNode), nameof(TextNode.CMYKToColorNodeDescription),
    typeof(TextNode))]
public class CMYKToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.CmykCyan), nameof(TextNode.CmykCyanDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float C
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.CmykMagenta), nameof(TextNode.CmykMagentaDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float M
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.CmykYellow), nameof(TextNode.CmykYellowDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.CmykKey), nameof(TextNode.CmykKeyDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float K
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.CmykToRgb(C / 100.0, M / 100.0, Y / 100.0, K / 100.0);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToCMYKNode), nameof(TextNode.ColorToCMYKNodeDescription),
    typeof(TextNode))]
public class ColorToCMYKNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.CmykCyan), nameof(TextNode.CmykCyanDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float C
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.CmykMagenta), nameof(TextNode.CmykMagentaDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float M
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.CmykYellow), nameof(TextNode.CmykYellowDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.CmykKey), nameof(TextNode.CmykKeyDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float K
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (c, m, y, k) = ColorConversionUtility.RgbToCmyk(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        C = (float)(c * 100);
        M = (float)(m * 100);
        Y = (float)(y * 100);
        K = (float)(k * 100);
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.YCbCrToColorNode), nameof(TextNode.YCbCrToColorNodeDescription),
    typeof(TextNode))]
public class YCbCrToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Luma), nameof(TextNode.LumaDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ChromaBlue), nameof(TextNode.ChromaBlueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 1, Default = 128f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Cb
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.ChromaRed), nameof(TextNode.ChromaRedDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 255, Digits = 1, Default = 128f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Cr
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.YCbCrToRgb(Y, Cb, Cr);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToYCbCrNode), nameof(TextNode.ColorToYCbCrNodeDescription),
    typeof(TextNode))]
public class ColorToYCbCrNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Luma), nameof(TextNode.LumaDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Y
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.ChromaBlue), nameof(TextNode.ChromaBlueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Cb
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.ChromaRed), nameof(TextNode.ChromaRedDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Cr
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (y, cb, cr) = ColorConversionUtility.RgbToYCbCr(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        Y = (float)y;
        Cb = (float)cb;
        Cr = (float)cr;
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.LabToColorNode), nameof(TextNode.LabToColorNodeDescription),
    typeof(TextNode))]
public class LabToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.LabLightness), nameof(TextNode.LabLightnessDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float L
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.LabA), nameof(TextNode.LabADescription), typeof(TextNode))]
    [NumberPortControl(Min = -128, Max = 127, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float A
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.LabB), nameof(TextNode.LabBDescription), typeof(TextNode))]
    [NumberPortControl(Min = -128, Max = 127, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.LabToRgb(L, A, B);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToLabNode), nameof(TextNode.ColorToLabNodeDescription),
    typeof(TextNode))]
public class ColorToLabNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.LabLightness), nameof(TextNode.LabLightnessDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float L
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.LabA), nameof(TextNode.LabADescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float A
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.LabB), nameof(TextNode.LabBDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float B
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (l, a, b) = ColorConversionUtility.RgbToLab(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        L = (float)l;
        A = (float)a;
        B = (float)b;
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.OklchToColorNode), nameof(TextNode.OklchToColorNodeDescription),
    typeof(TextNode))]
public class OklchToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.OklchLightness), nameof(TextNode.OklchLightnessDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 1, Digits = 3, Default = 1f)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float L
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.OklchChroma), nameof(TextNode.OklchChromaDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 0.5f, Digits = 3)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float C
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 360, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.OklchToRgb(L, C, H);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToOklchNode), nameof(TextNode.ColorToOklchNodeDescription),
    typeof(TextNode))]
public class ColorToOklchNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OklchLightness), nameof(TextNode.OklchLightnessDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float L
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.OklchChroma), nameof(TextNode.OklchChromaDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float C
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (l, c, h) = ColorConversionUtility.RgbToOklch(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        L = (float)l;
        C = (float)c;
        H = (float)h;
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.HWBToColorNode), nameof(TextNode.HWBToColorNodeDescription),
    typeof(TextNode))]
public class HWBToColorNode : NodeLogic
{
    [InputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 360, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.HwbWhiteness), nameof(TextNode.HwbWhitenessDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float W
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.HwbBlackness), nameof(TextNode.HwbBlacknessDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 100, Digits = 1)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Bk
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Result
    {
        get => GetOutput<Color>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (r, g, b) = ColorConversionUtility.HwbToRgb(H, W / 100.0, Bk / 100.0);
        Result = Color.FromRgb(ColorConversionUtility.ToByte(r), ColorConversionUtility.ToByte(g),
            ColorConversionUtility.ToByte(b));
        return Task.CompletedTask;
    }
}

[Node(typeof(ColorCategory), nameof(TextNode.ColorToHWBNode), nameof(TextNode.ColorToHWBNodeDescription),
    typeof(TextNode))]
public class ColorToHWBNode : NodeLogic
{
    [InputPort(nameof(TextNode.ColorValue), nameof(TextNode.ColorValueDescription), typeof(TextNode))]
    [ColorPortControl(DefaultColor = "#FFFFFFFF")]
    [PortColorSetting(nameof(Colors.Gold))]
    public Color Input
    {
        get => GetInput<Color>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.Hue), nameof(TextNode.HueDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float H
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.HwbWhiteness), nameof(TextNode.HwbWhitenessDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float W
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    [OutputPort(nameof(TextNode.HwbBlackness), nameof(TextNode.HwbBlacknessDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float Bk
    {
        get => GetOutput<float>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        var (h, w, bk) = ColorConversionUtility.RgbToHwb(Input.R / 255.0, Input.G / 255.0, Input.B / 255.0);
        H = (float)h;
        W = (float)(w * 100);
        Bk = (float)(bk * 100);
        return Task.CompletedTask;
    }
}