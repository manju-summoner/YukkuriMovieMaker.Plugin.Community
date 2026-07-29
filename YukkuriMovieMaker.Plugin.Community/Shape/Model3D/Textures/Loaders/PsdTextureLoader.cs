using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;

internal sealed class PsdTextureLoader : ITextureLoader
{
    private const uint PsdSignature = 0x38425053;
    private const ushort SupportedVersion = 1;
    private const ushort RgbColorMode = 3;
    private const ushort SupportedDepth = 8;
    private const ushort MinChannels = 3;
    private const ushort MaxChannels = 56;
    private const ushort RawCompression = 0;
    private const ushort RleCompression = 1;
    private const long MaxPixelCount = 512L * 1024 * 1024 / 4;
    private const int ChunkBytes = 65536;
    private const int MaxRunLength = 128;

    private static readonly byte[] ChannelToBgraOffset = [2, 1, 0, 3];

    public int Priority => 80;

    public bool CanLoad(string path) => path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase);

    public bool CanLoadRaw(string path) => path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase);

    public BitmapSource Load(string path)
    {
        using var raw = LoadRaw(path);
        var bmp = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Pixels, raw.Stride);
        if (bmp.CanFreeze) bmp.Freeze();
        return bmp;
    }

    public TextureRawData LoadRaw(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        uint signature = SwapUInt32(br.ReadUInt32());
        if (signature != PsdSignature)
        {
            throw new InvalidDataException("Invalid PSD Signature");
        }

        ushort version = SwapUInt16(br.ReadUInt16());
        if (version != SupportedVersion)
        {
            throw new NotSupportedException("Only PSD Version 1 supported");
        }

        br.ReadBytes(6);
        ushort channels = SwapUInt16(br.ReadUInt16());
        int height = SwapInt32(br.ReadInt32());
        int width = SwapInt32(br.ReadInt32());
        ushort depth = SwapUInt16(br.ReadUInt16());
        ushort mode = SwapUInt16(br.ReadUInt16());

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid dimensions: {width}x{height}");
        }

        long totalPixels = (long)width * height;
        if (totalPixels > MaxPixelCount)
        {
            throw new InvalidOperationException("Image dimensions too large");
        }

        if (mode != RgbColorMode)
        {
            throw new NotSupportedException($"PSD ColorMode {mode} not supported");
        }

        if (depth != SupportedDepth)
        {
            throw new NotSupportedException("Only 8-bit PSD supported");
        }

        if (channels < MinChannels || channels > MaxChannels)
        {
            throw new NotSupportedException($"PSD channel count {channels} not supported");
        }

        SkipSection(fs, br);
        SkipSection(fs, br);
        SkipSection(fs, br);

        ushort compression = SwapUInt16(br.ReadUInt16());

        int pixelCount = (int)totalPixels;
        int usedChannels = Math.Min(channels, (ushort)4);

        var rawData = new TextureRawData(width, height);
        try
        {
            if (compression == RawCompression)
            {
                ReadUncompressed(br, rawData.Pixels, pixelCount, usedChannels);
            }
            else if (compression == RleCompression)
            {
                ReadRleCompressed(fs, rawData.Pixels, width, height, channels, usedChannels);
            }
            else
            {
                throw new NotSupportedException("PSD Compression not supported");
            }

            if (usedChannels < 4) FillOpaqueAlpha(rawData.Pixels, pixelCount);
            return rawData;
        }
        catch
        {
            rawData.Dispose();
            throw;
        }
    }

    private static void SkipSection(FileStream fs, BinaryReader br)
    {
        uint len = SwapUInt32(br.ReadUInt32());
        if (len > 0) fs.Seek(len, SeekOrigin.Current);
    }

    private static void ReadUncompressed(BinaryReader br, byte[] pixels, int pixelCount, int usedChannels)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        try
        {
            for (int ch = 0; ch < usedChannels; ch++)
            {
                int destOffset = ChannelToBgraOffset[ch];
                int totalRead = 0;
                while (totalRead < pixelCount)
                {
                    int read = br.Read(buffer, 0, Math.Min(ChunkBytes, pixelCount - totalRead));
                    if (read == 0) throw new EndOfStreamException();

                    for (int i = 0; i < read; i++)
                    {
                        pixels[(totalRead + i) * 4 + destOffset] = buffer[i];
                    }
                    totalRead += read;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ReadRleCompressed(FileStream fs, byte[] pixels, int width, int height, int totalChannels, int usedChannels)
    {
        long tableBytes = (long)height * totalChannels * 2;
        if (tableBytes > fs.Length - fs.Position) throw new EndOfStreamException();
        if (tableBytes > int.MaxValue) throw new InvalidOperationException("Texture data exceeds memory limits");

        byte[] table = ArrayPool<byte>.Shared.Rent((int)tableBytes);
        byte[] run = ArrayPool<byte>.Shared.Rent(MaxRunLength);
        try
        {
            fs.ReadExactly(table, 0, (int)tableBytes);

            for (int ch = 0; ch < usedChannels; ch++)
            {
                int destOffset = ChannelToBgraOffset[ch];
                for (int y = 0; y < height; y++)
                {
                    int line = (ch * height + y) * 2;
                    int lineBytes = (table[line] << 8) | table[line + 1];
                    DecodeRleScanline(fs, pixels, (long)y * width, width, lineBytes, destOffset, run);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(run);
            ArrayPool<byte>.Shared.Return(table);
        }
    }

    private static void DecodeRleScanline(FileStream fs, byte[] pixels, long pixelStart, int width, int lineBytes, int destOffset, byte[] run)
    {
        int decoded = 0;
        int remaining = lineBytes;

        while (decoded < width)
        {
            if (remaining <= 0) throw new InvalidDataException("Invalid PSD RLE scanline");

            int b = fs.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            remaining--;

            if (b == 128) continue;

            if (b < 128)
            {
                int count = b + 1;
                if (count > remaining) throw new InvalidDataException("Invalid PSD RLE scanline");
                fs.ReadExactly(run, 0, count);
                remaining -= count;

                int store = Math.Min(count, width - decoded);
                for (int i = 0; i < store; i++)
                {
                    pixels[(pixelStart + decoded + i) * 4 + destOffset] = run[i];
                }
                decoded += count;
            }
            else
            {
                int count = 257 - b;
                int val = fs.ReadByte();
                if (val == -1) throw new EndOfStreamException();
                remaining--;

                byte fill = (byte)val;
                int store = Math.Min(count, width - decoded);
                for (int i = 0; i < store; i++)
                {
                    pixels[(pixelStart + decoded + i) * 4 + destOffset] = fill;
                }
                decoded += count;
            }
        }

        if (remaining > 0) fs.Seek(remaining, SeekOrigin.Current);
    }

    private static void FillOpaqueAlpha(byte[] pixels, int pixelCount)
    {
        for (int i = 0; i < pixelCount; i++)
        {
            pixels[i * 4 + 3] = 255;
        }
    }

    private static ushort SwapUInt16(ushort v) => (ushort)((v << 8) | (v >> 8));
    private static uint SwapUInt32(uint v) => (v << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | (v >> 24);
    private static int SwapInt32(int v) => (int)SwapUInt32((uint)v);
}
