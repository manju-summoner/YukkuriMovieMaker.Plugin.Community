using System.Collections.Immutable;
using System.IO;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

internal static class GrdParser
{
    internal const int Resolution = 256;

    internal static byte[]? ParseToPixels(string filePath, int gradientIndex = 0)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        Span<byte> sigBuf = stackalloc byte[4];
        stream.ReadExactly(sigBuf);
        if (sigBuf[0] != (byte)'8' || sigBuf[1] != (byte)'B' || sigBuf[2] != (byte)'G' || sigBuf[3] != (byte)'R')
            return null;

        var version = ReadU16(reader);

        return version >= 5
            ? ParseVersion5(reader, gradientIndex)
            : ParseLegacy(reader, version);
    }

    internal static GrdManifest ReadManifest(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            Span<byte> sigBuf = stackalloc byte[4];
            stream.ReadExactly(sigBuf);
            if (sigBuf[0] != (byte)'8' || sigBuf[1] != (byte)'B' || sigBuf[2] != (byte)'G' || sigBuf[3] != (byte)'R')
                return GrdManifest.Empty;

            var version = ReadU16(reader);
            if (version < 5) return GrdManifest.Empty;

            reader.BaseStream.Seek(4, SeekOrigin.Current);
            var descriptor = DescriptorReader.ReadDescriptor(reader);
            if (descriptor is null) return GrdManifest.Empty;

            if (!descriptor.TryGetValue("GrdL", out var grdlObj) ||
                grdlObj is not List<object?> grdList)
                return GrdManifest.Empty;

            var builder = ImmutableArray.CreateBuilder<GrdGradientEntry>(grdList.Count);
            for (var i = 0; i < grdList.Count; i++)
            {
                if (grdList[i] is not Dictionary<string, object?> item) continue;
                var grad = item.TryGetValue("Grad", out var g) && g is Dictionary<string, object?> gd
                    ? gd : item;
                var name = grad.TryGetValue("Nm  ", out var nm) && nm is string s && s.Length > 0
                    ? s : $"Gradient {i + 1}";
                builder.Add(new GrdGradientEntry(i, name, filePath));
            }

            return new GrdManifest(filePath, builder.ToImmutable());
        }
        catch
        {
            return GrdManifest.Empty;
        }
    }

    private static byte[]? ParseLegacy(BinaryReader reader, ushort version)
    {
        var count = ReadU16(reader);
        if (count == 0) return null;

        return ReadFirstSolidGradientPixelsLegacy(reader, version);
    }

    private static byte[]? ReadFirstSolidGradientPixelsLegacy(BinaryReader reader, ushort version)
    {
        SkipNameLegacy(reader, version);

        var gradType = ReadU16(reader);
        if (gradType != 0) return null;

        ReadU32(reader);

        var colorStopCount = ReadU16(reader);
        var colorStops = new (float Location, float Midpoint, byte R, byte G, byte B)[colorStopCount];
        for (var i = 0; i < colorStopCount; i++)
        {
            var location = ReadU32(reader) / 4096f;
            var midpoint = ReadU32(reader) / 100f;
            var model = ReadU16(reader);
            var c0 = ReadU16(reader);
            var c1 = ReadU16(reader);
            var c2 = ReadU16(reader);
            var c3 = ReadU16(reader);
            var (r, g, b) = ColorToRgb(model, c0, c1, c2, c3);
            colorStops[i] = (location, midpoint, r, g, b);
        }

        var transStopCount = ReadU16(reader);
        var transStops = new (float Location, float Midpoint, float Opacity)[transStopCount];
        for (var i = 0; i < transStopCount; i++)
        {
            var location = ReadU32(reader) / 4096f;
            var midpoint = ReadU32(reader) / 100f;
            var opacity = ReadU16(reader) / 65535f;
            transStops[i] = (location, midpoint, opacity);
        }

        return SampleToPixels(colorStops, transStops);
    }

    private static byte[]? ParseVersion5(BinaryReader reader, int gradientIndex)
    {
        reader.BaseStream.Seek(4, SeekOrigin.Current);

        var descriptor = DescriptorReader.ReadDescriptor(reader);
        if (descriptor is null) return null;

        if (!descriptor.TryGetValue("GrdL", out var grdlObj) ||
            grdlObj is not List<object?> grdList ||
            grdList.Count == 0)
            return null;

        var clampedIndex = Math.Clamp(gradientIndex, 0, grdList.Count - 1);

        if (grdList[clampedIndex] is not Dictionary<string, object?> item)
            return null;

        var gradientObj = ResolveGradientDescriptor(item);
        if (gradientObj is null) return null;

        var colorStops = ExtractColorStops(gradientObj);
        var transStops = ExtractTransparencyStops(gradientObj);

        if (colorStops is null || colorStops.Length == 0) return null;

        return SampleToPixels(
            colorStops,
            transStops ?? [(0f, 0.5f, 1f), (1f, 0.5f, 1f)]);
    }

    private static Dictionary<string, object?>? ResolveGradientDescriptor(
        Dictionary<string, object?> root)
    {
        if (root.TryGetValue("Grad", out var gradObj) &&
            gradObj is Dictionary<string, object?> gradDesc &&
            gradDesc.ContainsKey("Clrs"))
            return gradDesc;

        if (root.ContainsKey("Clrs"))
            return root;

        return null;
    }

    private static (float Location, float Midpoint, byte R, byte G, byte B)[]? ExtractColorStops(
        Dictionary<string, object?> gradient)
    {
        if (!gradient.TryGetValue("Clrs", out var clrsObj) ||
            clrsObj is not List<object?> clrsList)
            return null;

        var stops = new (float, float, byte, byte, byte)[clrsList.Count];
        for (var i = 0; i < clrsList.Count; i++)
        {
            if (clrsList[i] is not Dictionary<string, object?> stop) return null;

            var location = stop.TryGetValue("Lctn", out var lctn) && lctn is int loc
                ? loc / 4096f : 0f;
            var midpoint = stop.TryGetValue("Mdpn", out var mdpn) && mdpn is int mid
                ? mid / 100f : 0.5f;

            var (r, g, b) = ExtractColorFromStop(stop);
            stops[i] = (location, midpoint, r, g, b);
        }

        return stops;
    }

    private static (byte R, byte G, byte B) ExtractColorFromStop(Dictionary<string, object?> stop)
    {
        if (!stop.TryGetValue("Clr ", out var clrObj) ||
            clrObj is not Dictionary<string, object?> clr)
            return (0, 0, 0);

        if (!clr.TryGetValue("_class", out var classObj) || classObj is not string classId)
            return (0, 0, 0);

        return classId switch
        {
            "RGBC" => ExtractRgbc(clr),
            "HSBC" => ExtractHsbc(clr),
            "CMYC" => ExtractCmyc(clr),
            "LbCl" => ExtractLabc(clr),
            "Grsc" => ExtractGrsc(clr),
            _ => (0, 0, 0)
        };
    }

    private static (byte R, byte G, byte B) ExtractRgbc(Dictionary<string, object?> clr)
    {
        var rd = GetDouble(clr, "Rd  ");
        var gn = GetDouble(clr, "Grn ");
        var bl = GetDouble(clr, "Bl  ");
        return (
            (byte)Math.Clamp(Math.Round(rd), 0, 255),
            (byte)Math.Clamp(Math.Round(gn), 0, 255),
            (byte)Math.Clamp(Math.Round(bl), 0, 255));
    }

    private static (byte R, byte G, byte B) ExtractHsbc(Dictionary<string, object?> clr)
    {
        var h = GetDouble(clr, "H   ") / 360.0;
        var s = GetDouble(clr, "Strt") / 100.0;
        var bv = GetDouble(clr, "Brgh") / 100.0;
        return HsbToRgbF(h, s, bv);
    }

    private static (byte R, byte G, byte B) ExtractCmyc(Dictionary<string, object?> clr)
    {
        var c = GetDouble(clr, "Cyn ") / 100.0;
        var m = GetDouble(clr, "Mgnt") / 100.0;
        var y = GetDouble(clr, "Ylw ") / 100.0;
        var k = GetDouble(clr, "Blck") / 100.0;
        return (
            (byte)((1.0 - c) * (1.0 - k) * 255.0),
            (byte)((1.0 - m) * (1.0 - k) * 255.0),
            (byte)((1.0 - y) * (1.0 - k) * 255.0));
    }

    private static (byte R, byte G, byte B) ExtractLabc(Dictionary<string, object?> clr)
    {
        var l = GetDouble(clr, "Lmnc");
        var a = GetDouble(clr, "A   ");
        var bv = GetDouble(clr, "B   ");
        return LabToRgbF(l, a, bv);
    }

    private static (byte R, byte G, byte B) ExtractGrsc(Dictionary<string, object?> clr)
    {
        var gray = GetDouble(clr, "Gry ");
        var v = (byte)Math.Clamp(Math.Round(gray / 100.0 * 255.0), 0, 255);
        return (v, v, v);
    }

    private static double GetDouble(Dictionary<string, object?> dict, string key)
    {
        if (dict.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is (string _, double dv)) return dv;
        }
        return 0.0;
    }

    private static (float Location, float Midpoint, float Opacity)[]? ExtractTransparencyStops(
        Dictionary<string, object?> gradient)
    {
        if (!gradient.TryGetValue("Trns", out var trnsObj) ||
            trnsObj is not List<object?> trnsList)
            return null;

        var stops = new (float, float, float)[trnsList.Count];
        for (var i = 0; i < trnsList.Count; i++)
        {
            if (trnsList[i] is not Dictionary<string, object?> stop) continue;

            var location = stop.TryGetValue("Lctn", out var lctn) && lctn is int loc
                ? loc / 4096f : 0f;
            var midpoint = stop.TryGetValue("Mdpn", out var mdpn) && mdpn is int mid
                ? mid / 100f : 0.5f;
            var opacity = 1f;
            if (stop.TryGetValue("Opct", out var opctObj))
            {
                if (opctObj is (string _, double dv))
                    opacity = (float)(dv / 100.0);
                else if (opctObj is double dv2)
                    opacity = (float)(dv2 / 100.0);
            }
            stops[i] = (location, midpoint, opacity);
        }

        return stops;
    }

    private static (byte R, byte G, byte B) HsbToRgbF(double h, double s, double b)
    {
        if (s < 1e-6)
        {
            var v = (byte)(b * 255.0);
            return (v, v, v);
        }

        var hf = h * 6.0;
        var sector = (int)hf % 6;
        var f = hf - Math.Floor(hf);
        var p = b * (1.0 - s);
        var q = b * (1.0 - f * s);
        var tv = b * (1.0 - (1.0 - f) * s);

        var (r, g, bv) = sector switch
        {
            0 => (b, tv, p),
            1 => (q, b, p),
            2 => (p, b, tv),
            3 => (p, q, b),
            4 => (tv, p, b),
            _ => (b, p, q)
        };

        return ((byte)(r * 255.0), (byte)(g * 255.0), (byte)(bv * 255.0));
    }

    private static (byte R, byte G, byte B) LabToRgbF(double l, double a, double b)
    {
        var fy = (l + 16.0) / 116.0;
        var fx = a / 500.0 + fy;
        var fz = fy - b / 200.0;

        static double F(double t) => t > 0.206897 ? t * t * t : (t - 16.0 / 116.0) / 7.787;

        var x = 0.95047 * F(fx);
        var y = 1.00000 * F(fy);
        var z = 1.08883 * F(fz);

        var r = x * 3.2406 + y * -1.5372 + z * -0.4986;
        var g = x * -0.9689 + y * 1.8758 + z * 0.0415;
        var bv = x * 0.0557 + y * -0.2040 + z * 1.0570;

        static double Gamma(double v) =>
            v > 0.0031308 ? 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055 : 12.92 * v;

        return (
            (byte)(Math.Clamp(Gamma(r), 0.0, 1.0) * 255.0),
            (byte)(Math.Clamp(Gamma(g), 0.0, 1.0) * 255.0),
            (byte)(Math.Clamp(Gamma(bv), 0.0, 1.0) * 255.0));
    }

    private static void SkipNameLegacy(BinaryReader reader, ushort version)
    {
        if (version >= 5)
        {
            var charCount = ReadU32(reader);
            reader.BaseStream.Seek(charCount * 2, SeekOrigin.Current);
        }
        else
        {
            var byteCount = ReadU16(reader);
            reader.BaseStream.Seek(byteCount, SeekOrigin.Current);
        }
    }

    internal static byte[] SampleToPixels(
        (float Location, float Midpoint, byte R, byte G, byte B)[] colorStops,
        (float Location, float Midpoint, float Opacity)[] transStops)
    {
        var pixels = new byte[Resolution * 4];
        for (var i = 0; i < Resolution; i++)
        {
            var t = i / (Resolution - 1f);
            var (r, g, b) = SampleColor(colorStops, t);
            var a = SampleOpacity(transStops, t);

            pixels[i * 4 + 0] = (byte)Math.Round(b * a * 255f);
            pixels[i * 4 + 1] = (byte)Math.Round(g * a * 255f);
            pixels[i * 4 + 2] = (byte)Math.Round(r * a * 255f);
            pixels[i * 4 + 3] = (byte)Math.Round(a * 255f);
        }
        return pixels;
    }

    private static (float R, float G, float B) SampleColor(
        (float Location, float Midpoint, byte R, byte G, byte B)[] stops, float t)
    {
        if (stops.Length == 0) return (0f, 0f, 0f);
        if (stops.Length == 1) return (stops[0].R / 255f, stops[0].G / 255f, stops[0].B / 255f);
        if (t <= stops[0].Location) return (stops[0].R / 255f, stops[0].G / 255f, stops[0].B / 255f);
        if (t >= stops[^1].Location) return (stops[^1].R / 255f, stops[^1].G / 255f, stops[^1].B / 255f);

        for (var i = 0; i < stops.Length - 1; i++)
        {
            var left = stops[i];
            var right = stops[i + 1];
            if (t < left.Location || t > right.Location) continue;

            var span = right.Location - left.Location;
            if (span < 1e-6f) return (right.R / 255f, right.G / 255f, right.B / 255f);

            var local = (t - left.Location) / span;
            var adj = AdjustMidpoint(local, left.Midpoint);
            return (
                Lerp(left.R / 255f, right.R / 255f, adj),
                Lerp(left.G / 255f, right.G / 255f, adj),
                Lerp(left.B / 255f, right.B / 255f, adj));
        }

        return (stops[^1].R / 255f, stops[^1].G / 255f, stops[^1].B / 255f);
    }

    private static float SampleOpacity(
        (float Location, float Midpoint, float Opacity)[] stops, float t)
    {
        if (stops.Length == 0) return 1f;
        if (stops.Length == 1) return stops[0].Opacity;
        if (t <= stops[0].Location) return stops[0].Opacity;
        if (t >= stops[^1].Location) return stops[^1].Opacity;

        for (var i = 0; i < stops.Length - 1; i++)
        {
            var left = stops[i];
            var right = stops[i + 1];
            if (t < left.Location || t > right.Location) continue;

            var span = right.Location - left.Location;
            if (span < 1e-6f) return right.Opacity;

            var local = (t - left.Location) / span;
            return Lerp(left.Opacity, right.Opacity, AdjustMidpoint(local, left.Midpoint));
        }

        return stops[^1].Opacity;
    }

    private static float AdjustMidpoint(float local, float mid)
    {
        if (local <= mid)
            return mid > 0f ? 0.5f * local / mid : 0.5f;
        return (1f - mid) > 0f ? 0.5f + 0.5f * (local - mid) / (1f - mid) : 1f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static (byte R, byte G, byte B) ColorToRgb(ushort model, ushort c0, ushort c1, ushort c2, ushort c3)
    {
        return model switch
        {
            0 => ((byte)(c0 >> 8), (byte)(c1 >> 8), (byte)(c2 >> 8)),
            1 => HsbToRgb(c0, c1, c2),
            2 => CmykToRgb(c0, c1, c2, c3),
            3 => LabToRgb(c0, c1, c2),
            7 => ((byte)(c0 >> 8), (byte)(c0 >> 8), (byte)(c0 >> 8)),
            _ => (0, 0, 0)
        };
    }

    private static (byte R, byte G, byte B) HsbToRgb(ushort h, ushort s, ushort b)
    {
        var hf = h / 65535f * 360f;
        var sf = s / 65535f;
        var bf = b / 65535f;

        if (sf < 1e-6f)
        {
            var v = (byte)(bf * 255f);
            return (v, v, v);
        }

        var sector = (int)(hf / 60f) % 6;
        var f = hf / 60f - MathF.Floor(hf / 60f);
        var p = bf * (1f - sf);
        var q = bf * (1f - f * sf);
        var tv = bf * (1f - (1f - f) * sf);

        var (r, g, bv) = sector switch
        {
            0 => (bf, tv, p),
            1 => (q, bf, p),
            2 => (p, bf, tv),
            3 => (p, q, bf),
            4 => (tv, p, bf),
            _ => (bf, p, q)
        };

        return ((byte)(r * 255f), (byte)(g * 255f), (byte)(bv * 255f));
    }

    private static (byte R, byte G, byte B) CmykToRgb(ushort c, ushort m, ushort y, ushort k)
    {
        var cf = c / 65535f;
        var mf = m / 65535f;
        var yf = y / 65535f;
        var kf = k / 65535f;
        return (
            (byte)((1f - cf) * (1f - kf) * 255f),
            (byte)((1f - mf) * (1f - kf) * 255f),
            (byte)((1f - yf) * (1f - kf) * 255f));
    }

    private static (byte R, byte G, byte B) LabToRgb(ushort l, ushort a, ushort b)
    {
        var lf = l / 65535f * 100f;
        var af = a / 65535f * 255f - 128f;
        var bf = b / 65535f * 255f - 128f;

        var fy = (lf + 16f) / 116f;
        var fx = af / 500f + fy;
        var fz = fy - bf / 200f;

        static float F(float t) => t > 0.206897f ? t * t * t : (t - 16f / 116f) / 7.787f;

        var x = 0.95047f * F(fx);
        var y = 1.00000f * F(fy);
        var z = 1.08883f * F(fz);

        var r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
        var g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
        var bv = x * 0.0557f + y * -0.2040f + z * 1.0570f;

        static float Gamma(float v) =>
            v > 0.0031308f ? 1.055f * MathF.Pow(v, 1f / 2.4f) - 0.055f : 12.92f * v;

        return (
            (byte)(Math.Clamp(Gamma(r), 0f, 1f) * 255f),
            (byte)(Math.Clamp(Gamma(g), 0f, 1f) * 255f),
            (byte)(Math.Clamp(Gamma(bv), 0f, 1f) * 255f));
    }

    private static ushort ReadU16(BinaryReader r)
    {
        Span<byte> buf = stackalloc byte[2];
        r.BaseStream.ReadExactly(buf);
        return (ushort)((buf[0] << 8) | buf[1]);
    }

    private static uint ReadU32(BinaryReader r)
    {
        Span<byte> buf = stackalloc byte[4];
        r.BaseStream.ReadExactly(buf);
        return (uint)((buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3]);
    }
}
