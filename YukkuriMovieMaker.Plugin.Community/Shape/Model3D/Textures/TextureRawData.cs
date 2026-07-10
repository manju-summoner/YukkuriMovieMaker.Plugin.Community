using System.Buffers;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

internal sealed class TextureRawData : IDisposable
{
    private byte[]? _pixels;
    private readonly bool _fromPool;

    public byte[] Pixels => _pixels ?? throw new ObjectDisposedException(nameof(TextureRawData));
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public int DataLength { get; }

    public TextureRawData(int width, int height)
    {
        Width = width;
        Height = height;
        Stride = width * 4;
        DataLength = Stride * height;
        _fromPool = true;
        _pixels = ArrayPool<byte>.Shared.Rent(DataLength);
    }

    public TextureRawData(byte[] pixels, int width, int height)
    {
        Width = width;
        Height = height;
        Stride = width * 4;
        DataLength = Stride * height;
        _fromPool = false;
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
    }

    public byte[]? TryGetPixels() => Volatile.Read(ref _pixels);

    public TextureRawData ToNonPooled()
    {
        var src = Pixels;
        var dst = new byte[DataLength];
        Buffer.BlockCopy(src, 0, dst, 0, DataLength);
        return new TextureRawData(dst, Width, Height);
    }

    public void Dispose()
    {
        var p = Interlocked.Exchange(ref _pixels, null);
        if (_fromPool && p != null)
        {
            ArrayPool<byte>.Shared.Return(p);
        }
    }
}
