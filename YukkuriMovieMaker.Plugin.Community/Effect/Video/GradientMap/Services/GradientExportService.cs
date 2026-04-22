using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public static class GradientExportService
{
    public const int GradientResolution = 256;

    public static void ExportAsGrd(string filePath, string name, GradientColorStop[] stops)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: false);
        WriteGrd(writer, name, stops);
    }

    public static void ExportAsPng(string filePath, GradientColorStop[] stops)
    {
        var pixels = RasterizeGradient(stops);
        var source = BitmapSource.Create(
            GradientResolution, 1, 96, 96,
            PixelFormats.Pbgra32, null,
            pixels, GradientResolution * 4);
        source.Freeze();

        using var stream = File.Create(filePath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
    }

    public static byte[] RasterizeGradient(GradientColorStop[] stops)
    {
        var pixels = new byte[GradientResolution * 4];
        for (var i = 0; i < GradientResolution; i++)
        {
            var t = i / (GradientResolution - 1f);
            var (r, g, b, a) = SampleAt(stops, t);
            var af = a / 255f;
            pixels[i * 4 + 0] = (byte)MathF.Round(b * af);
            pixels[i * 4 + 1] = (byte)MathF.Round(g * af);
            pixels[i * 4 + 2] = (byte)MathF.Round(r * af);
            pixels[i * 4 + 3] = a;
        }
        return pixels;
    }

    private static void WriteGrd(BinaryWriter w, string name, GradientColorStop[] stops)
    {
        w.Write("8BGR"u8);

        WriteU16(w, 3);
        WriteU16(w, 1);

        var truncated = name.Length > 31 ? name.AsSpan(0, 31) : name.AsSpan();
        var nameByteCount = System.Text.Encoding.ASCII.GetByteCount(truncated);
        Span<byte> nameBytes = nameByteCount <= 64 ? stackalloc byte[nameByteCount] : new byte[nameByteCount];
        System.Text.Encoding.ASCII.GetBytes(truncated, nameBytes);
        WriteU16(w, (ushort)nameByteCount);
        w.Write(nameBytes);

        WriteU16(w, 0);
        WriteU32(w, 4096);
        WriteU16(w, (ushort)stops.Length);

        foreach (var s in stops)
        {
            WriteU32(w, (uint)MathF.Round(s.Position * 4096f));
            WriteU32(w, 50);
            WriteU16(w, 0);
            WriteU16(w, (ushort)(s.R << 8));
            WriteU16(w, (ushort)(s.G << 8));
            WriteU16(w, (ushort)(s.B << 8));
            WriteU16(w, 0);
        }

        WriteU16(w, (ushort)stops.Length);
        foreach (var s in stops)
        {
            WriteU32(w, (uint)MathF.Round(s.Position * 4096f));
            WriteU32(w, 50);
            WriteU16(w, (ushort)(s.A * 257));
        }
    }

    private static (byte R, byte G, byte B, byte A) SampleAt(GradientColorStop[] stops, float t)
    {
        if (stops.Length == 0) return (0, 0, 0, 255);
        if (t <= stops[0].Position) return (stops[0].R, stops[0].G, stops[0].B, stops[0].A);
        if (t >= stops[^1].Position) return (stops[^1].R, stops[^1].G, stops[^1].B, stops[^1].A);

        for (var i = 0; i < stops.Length - 1; i++)
        {
            var l = stops[i];
            var r = stops[i + 1];
            if (t < l.Position || t > r.Position) continue;
            var span = r.Position - l.Position;
            if (span < 1e-6f) return (r.R, r.G, r.B, r.A);
            var f = (t - l.Position) / span;
            return (Lerp(l.R, r.R, f), Lerp(l.G, r.G, f), Lerp(l.B, r.B, f), Lerp(l.A, r.A, f));
        }
        return (stops[^1].R, stops[^1].G, stops[^1].B, stops[^1].A);
    }

    private static byte Lerp(byte a, byte b, float t) => (byte)MathF.Round(a + (b - a) * t);

    private static void WriteU16(BinaryWriter w, ushort v)
    {
        w.Write((byte)(v >> 8));
        w.Write((byte)(v & 0xFF));
    }

    private static void WriteU32(BinaryWriter w, uint v)
    {
        w.Write((byte)(v >> 24));
        w.Write((byte)((v >> 16) & 0xFF));
        w.Write((byte)((v >> 8) & 0xFF));
        w.Write((byte)(v & 0xFF));
    }
}
