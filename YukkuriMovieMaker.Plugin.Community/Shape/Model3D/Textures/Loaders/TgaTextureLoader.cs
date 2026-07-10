using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;

internal sealed class TgaTextureLoader : ITextureLoader
{
    public int Priority => 100;

    public bool CanLoad(string path) => path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);

    public bool CanLoadRaw(string path) => path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);

    public BitmapSource Load(string path)
    {
        using var raw = LoadRaw(path);
        var bmp = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Pixels, raw.Stride);
        if (bmp.CanFreeze) bmp.Freeze();
        return bmp;
    }

    public TextureRawData LoadRaw(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        byte idLength = br.ReadByte();
        byte colorMapType = br.ReadByte();
        byte imageType = br.ReadByte();
        br.ReadBytes(2);
        ushort colorMapLength = br.ReadUInt16();
        byte colorMapEntrySize = br.ReadByte();
        br.ReadInt16();
        br.ReadInt16();
        short width = br.ReadInt16();
        short height = br.ReadInt16();
        byte pixelDepth = br.ReadByte();
        byte imageDescriptor = br.ReadByte();

        if (imageType != 2 && imageType != 10)
        {
            throw new NotSupportedException($"TGA ImageType {imageType} not supported");
        }

        if (pixelDepth != 24 && pixelDepth != 32)
        {
            throw new NotSupportedException($"TGA PixelDepth {pixelDepth} not supported");
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid TGA dimensions: {width}x{height}");
        }

        if (idLength > 0) br.ReadBytes(idLength);
        if (colorMapType == 1)
        {
            int skip = colorMapLength * colorMapEntrySize / 8;
            br.ReadBytes(skip);
        }

        int stride = width * 4;
        int pixelCount = width * height;
        var rawData = new TextureRawData(width, height);
        var pixels = rawData.Pixels;
        int rawIdx = 0;

        try
        {
            if (imageType == 2)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    pixels[rawIdx++] = br.ReadByte();
                    pixels[rawIdx++] = br.ReadByte();
                    pixels[rawIdx++] = br.ReadByte();
                    pixels[rawIdx++] = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                }
            }
            else if (imageType == 10)
            {
                int currentPixel = 0;
                while (currentPixel < pixelCount)
                {
                    byte header = br.ReadByte();
                    int count = Math.Min((header & 0x7F) + 1, pixelCount - currentPixel);
                    if ((header & 0x80) != 0)
                    {
                        byte b = br.ReadByte();
                        byte g = br.ReadByte();
                        byte r = br.ReadByte();
                        byte a = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                        for (int i = 0; i < count; i++)
                        {
                            pixels[rawIdx++] = b;
                            pixels[rawIdx++] = g;
                            pixels[rawIdx++] = r;
                            pixels[rawIdx++] = a;
                            currentPixel++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            pixels[rawIdx++] = br.ReadByte();
                            pixels[rawIdx++] = br.ReadByte();
                            pixels[rawIdx++] = br.ReadByte();
                            pixels[rawIdx++] = pixelDepth == 32 ? br.ReadByte() : (byte)255;
                            currentPixel++;
                        }
                    }
                }
            }

            bool isTopLeft = (imageDescriptor & 0x20) != 0;
            if (!isTopLeft)
            {
                byte[] tempRow = ArrayPool<byte>.Shared.Rent(stride);
                try
                {
                    for (int y = 0; y < height / 2; y++)
                    {
                        int topOffset = y * stride;
                        int bottomOffset = (height - 1 - y) * stride;
                        Buffer.BlockCopy(pixels, topOffset, tempRow, 0, stride);
                        Buffer.BlockCopy(pixels, bottomOffset, pixels, topOffset, stride);
                        Buffer.BlockCopy(tempRow, 0, pixels, bottomOffset, stride);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempRow);
                }
            }

            return rawData;
        }
        catch
        {
            rawData.Dispose();
            throw;
        }
    }
}
