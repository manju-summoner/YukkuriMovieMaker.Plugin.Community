using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;

internal sealed class PsdTextureLoader : ITextureLoader
{
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
        if (signature != 0x38425053)
        {
            throw new InvalidDataException("Invalid PSD Signature");
        }

        ushort version = SwapUInt16(br.ReadUInt16());
        if (version != 1)
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
        if (totalPixels > 1024L * 1024 * 256)
        {
            throw new InvalidOperationException("Image dimensions too large");
        }

        if (mode != 3)
        {
            throw new NotSupportedException($"PSD ColorMode {mode} not supported");
        }

        if (depth != 8)
        {
            throw new NotSupportedException("Only 8-bit PSD supported");
        }

        if (channels < 3 || channels > 56)
        {
            throw new NotSupportedException($"PSD channel count {channels} not supported");
        }

        SkipSection(fs, br);
        SkipSection(fs, br);
        SkipSection(fs, br);

        ushort compression = SwapUInt16(br.ReadUInt16());

        int pixelCount = (int)totalPixels;
        int usedChannels = Math.Min(channels, (ushort)4);
        long requiredBytes = (long)pixelCount * usedChannels;

        if (requiredBytes > int.MaxValue - 1024)
        {
            throw new InvalidOperationException("Texture data exceeds memory limits");
        }

        byte[] channelData = ArrayPool<byte>.Shared.Rent((int)requiredBytes);
        TextureRawData? rawData = null;

        try
        {
            if (compression == 0)
            {
                ReadUncompressed(br, channelData, pixelCount, channels, usedChannels);
            }
            else if (compression == 1)
            {
                ReadRleCompressed(br, fs, channelData, width, height, channels, usedChannels, pixelCount);
            }
            else
            {
                throw new NotSupportedException("PSD Compression not supported");
            }

            rawData = new TextureRawData(width, height);
            byte[] pixels = rawData.Pixels;
            int rOffset = 0;
            int gOffset = pixelCount;
            int bOffset = pixelCount * 2;
            int aOffset = usedChannels > 3 ? pixelCount * 3 : -1;

            for (int i = 0; i < pixelCount; i++)
            {
                int dest = i * 4;
                pixels[dest] = channelData[bOffset + i];
                pixels[dest + 1] = channelData[gOffset + i];
                pixels[dest + 2] = channelData[rOffset + i];
                pixels[dest + 3] = aOffset >= 0 ? channelData[aOffset + i] : (byte)255;
            }

            return rawData;
        }
        catch
        {
            rawData?.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(channelData);
        }
    }

    private static void SkipSection(FileStream fs, BinaryReader br)
    {
        uint len = SwapUInt32(br.ReadUInt32());
        if (len > 0) fs.Seek(len, SeekOrigin.Current);
    }

    private static void ReadUncompressed(BinaryReader br, byte[] channelData, int pixelCount, int totalChannels, int usedChannels)
    {
        for (int ch = 0; ch < usedChannels; ch++)
        {
            int offset = ch * pixelCount;
            int totalRead = 0;
            while (totalRead < pixelCount)
            {
                int read = br.Read(channelData, offset + totalRead, pixelCount - totalRead);
                if (read == 0) throw new EndOfStreamException();
                totalRead += read;
            }
        }

        if (totalChannels > usedChannels)
        {
            long skipBytes = (long)(totalChannels - usedChannels) * pixelCount;
            br.BaseStream.Seek(skipBytes, SeekOrigin.Current);
        }
    }

    private static void ReadRleCompressed(BinaryReader br, FileStream fs, byte[] channelData, int width, int height, int totalChannels, int usedChannels, int pixelCount)
    {
        int totalScanlines = height * totalChannels;
        long rleCountBytes = (long)totalScanlines * 2;
        fs.Seek(rleCountBytes, SeekOrigin.Current);

        for (int ch = 0; ch < totalChannels; ch++)
        {
            bool store = ch < usedChannels;

            for (int y = 0; y < height; y++)
            {
                int decoded = 0;
                int rowOffset = store ? (ch * pixelCount) + (y * width) : 0;

                while (decoded < width)
                {
                    int b = fs.ReadByte();
                    if (b == -1) throw new EndOfStreamException();
                    byte lenByte = (byte)b;

                    if (lenByte == 128) continue;

                    if (lenByte < 128)
                    {
                        int count = lenByte + 1;
                        int remaining = width - decoded;
                        if (count > remaining) count = remaining;

                        if (store)
                        {
                            int destOffset = rowOffset + decoded;
                            int read = br.Read(channelData, destOffset, count);
                            if (read != count) throw new EndOfStreamException();
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (fs.ReadByte() == -1) throw new EndOfStreamException();
                            }
                        }

                        decoded += count;
                    }
                    else
                    {
                        int count = (lenByte ^ 0xFF) + 2;
                        int remaining = width - decoded;
                        if (count > remaining) count = remaining;

                        int val = fs.ReadByte();
                        if (val == -1) throw new EndOfStreamException();

                        if (store)
                        {
                            byte bVal = (byte)val;
                            int start = rowOffset + decoded;
                            int end = start + count;

                            for (int k = start; k < end; k++)
                            {
                                channelData[k] = bVal;
                            }
                        }

                        decoded += count;
                    }
                }
            }
        }
    }

    private static ushort SwapUInt16(ushort v) => (ushort)((v << 8) | (v >> 8));
    private static uint SwapUInt32(uint v) => (v << 24) | ((v & 0xFF00) << 8) | ((v >> 8) & 0xFF00) | (v >> 24);
    private static int SwapInt32(int v) => (int)SwapUInt32((uint)v);
}
