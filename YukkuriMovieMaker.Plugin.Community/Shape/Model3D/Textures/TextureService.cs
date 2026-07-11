using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures.Loaders;
using static YukkuriMovieMaker.Plugin.Community.Shape.Model3D.DisposeUtility;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

internal sealed class TextureService : ITextureService
{
    private const long RawCacheMaxBytes = 512L * 1024 * 1024;
    private const long GpuCacheMaxBytes = 1024L * 1024 * 1024;

    private sealed record CachedGpuTexture(ID3D11Texture2D Texture, bool HasTransparency);

    private static readonly BoundedLruCache<string, TextureRawData> RawDataCache = new(RawCacheMaxBytes);
    private static readonly BoundedLruCache<(nint DevicePtr, string Path), CachedGpuTexture> GpuTextureCache = new(GpuCacheMaxBytes);
    private static readonly ConcurrentDictionary<nint, int> DeviceRefCounts = new();

    private readonly List<ITextureLoader> _loaders = [];
    private readonly HashSet<nint> _trackedDevices = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    public TextureService()
    {
        RegisterLoader(new DdsTextureLoader());
        RegisterLoader(new PsdTextureLoader());
        RegisterLoader(new TgaTextureLoader());
        RegisterLoader(new StandardTextureLoader());
    }

    private void RegisterLoader(ITextureLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextureService));
            _loaders.Add(loader);
        }
    }

    public BitmapSource Load(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        path = Path.GetFullPath(path).ToLowerInvariant();

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextureService));
        }

        if (AcquireRawPixels(path) is { } raw)
        {
            var bmp = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Pixels, raw.Stride);
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }

        ITextureLoader? loader = FindLoader(path);
        if (loader == null)
        {
            throw new NotSupportedException($"No suitable loader found for texture: {path}");
        }

        var bitmap = loader.Load(path);
        if (bitmap.CanFreeze && !bitmap.IsFrozen) bitmap.Freeze();
        return bitmap;
    }

    public (ID3D11ShaderResourceView? Srv, long GpuBytes, bool HasTransparency) CreateShaderResourceView(string path, ID3D11Device device)
    {
        if (string.IsNullOrEmpty(path)) return (null, 0, false);
        if (device == null) return (null, 0, false);

        lock (_lock)
        {
            if (_disposed) return (null, 0, false);
        }

        var devicePtr = device.NativePointer;
        TrackDevice(devicePtr);

        path = Path.GetFullPath(path).ToLowerInvariant();
        var key = (devicePtr, MakeContentKey(path));

        if (GpuTextureCache.TryGetValue(key, out var cachedTex, out long cachedBytes))
        {
            try
            {
                var srv = device.CreateShaderResourceView(cachedTex.Texture);
                return (srv, cachedBytes, cachedTex.HasTransparency);
            }
            catch
            {
                if (GpuTextureCache.TryRemove(key, out var stale))
                {
                    SafeDispose(stale.Texture);
                }
            }
        }

        if (AcquireRawPixels(path) is not { } rawData) return (null, 0, false);

        return CreateAndCacheGpuTexture(key, rawData, device);
    }

    public void EvictGpuTexture(string path, ID3D11Device device)
    {
        if (string.IsNullOrEmpty(path) || device == null) return;

        path = Path.GetFullPath(path).ToLowerInvariant();
        if (GpuTextureCache.TryRemove((device.NativePointer, MakeContentKey(path)), out var tex))
        {
            SafeDispose(tex.Texture);
        }
    }

    private static string MakeContentKey(string path)
    {
        long ticks = 0;
        try { ticks = File.GetLastWriteTimeUtc(path).Ticks; } catch { }
        return path + "|" + ticks;
    }

    private (byte[] Pixels, int Width, int Height, int Stride)? AcquireRawPixels(string path)
    {
        string contentKey = MakeContentKey(path);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var raw = EnsureRawDataCached(path, contentKey);
            if (raw == null) return null;

            var pixels = raw.TryGetPixels();
            if (pixels != null) return (pixels, raw.Width, raw.Height, raw.Stride);
        }
        return null;
    }

    private unsafe (ID3D11ShaderResourceView? Srv, long GpuBytes, bool HasTransparency) CreateAndCacheGpuTexture(
        (nint DevicePtr, string Path) key, (byte[] Pixels, int Width, int Height, int Stride) rawData, ID3D11Device device)
    {
        int width = rawData.Width;
        int height = rawData.Height;
        int stride = rawData.Stride;
        long gpuBytes = (long)width * height * 4;
        bool hasTransparency = HasTransparentPixels(rawData);

        var texDesc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource
        };

        fixed (byte* p = rawData.Pixels)
        {
            var data = new SubresourceData(p, stride);
            var entry = new CachedGpuTexture(device.CreateTexture2D(texDesc, new[] { data }), hasTransparency);

            var cached = GpuTextureCache.GetOrAdd(key, gpuBytes, _ => entry);

            if (!ReferenceEquals(cached, entry))
            {
                entry.Texture.Dispose();
            }

            try
            {
                var srv = device.CreateShaderResourceView(cached.Texture);
                return (srv, gpuBytes, cached.HasTransparency);
            }
            catch
            {
                if (GpuTextureCache.TryRemove(key, out var stale))
                {
                    SafeDispose(stale.Texture);
                }
                return (null, 0, false);
            }
        }
    }

    private static bool HasTransparentPixels((byte[] Pixels, int Width, int Height, int Stride) rawData)
    {
        var pixels = rawData.Pixels;
        long length = (long)rawData.Stride * rawData.Height;
        for (long i = 3; i < length; i += 4)
        {
            if (pixels[i] < byte.MaxValue) return true;
        }
        return false;
    }

    private TextureRawData? EnsureRawDataCached(string path, string contentKey)
    {
        if (RawDataCache.TryGetValue(contentKey, out var cached))
        {
            return cached;
        }

        ITextureLoader? loader = FindLoader(path);
        if (loader == null) return null;

        if (loader.CanLoadRaw(path))
        {
            return DecodeAndCacheRaw(path, contentKey, loader);
        }

        return DecodeAndCacheFromBitmap(path, contentKey, loader);
    }

    private static TextureRawData? DecodeAndCacheRaw(string path, string contentKey, ITextureLoader loader)
    {
        using var pooled = loader.LoadRaw(path);

        long bytes = pooled.DataLength;
        if (bytes > RawCacheMaxBytes) return null;

        var persistent = pooled.ToNonPooled();
        var result = RawDataCache.GetOrAdd(contentKey, bytes, _ => persistent);

        if (!ReferenceEquals(result, persistent))
        {
            persistent.Dispose();
        }

        return result;
    }

    private static TextureRawData? DecodeAndCacheFromBitmap(string path, string contentKey, ITextureLoader loader)
    {
        BitmapSource bitmapSource;
        try
        {
            bitmapSource = loader.Load(path);
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        if (bitmapSource.CanFreeze && !bitmapSource.IsFrozen) bitmapSource.Freeze();
        var converted = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        long requiredBytes = (long)stride * height;
        if (requiredBytes <= 0 || requiredBytes > RawCacheMaxBytes) return null;

        int requiredSize = (int)requiredBytes;
        byte[] pooledBuf = ArrayPool<byte>.Shared.Rent(requiredSize);
        try
        {
            converted.CopyPixels(pooledBuf, stride, 0);

            var pixels = new byte[requiredSize];
            Buffer.BlockCopy(pooledBuf, 0, pixels, 0, requiredSize);
            var rawData = new TextureRawData(pixels, width, height);

            var result = RawDataCache.GetOrAdd(contentKey, requiredSize, _ => rawData);

            if (!ReferenceEquals(result, rawData))
            {
                rawData.Dispose();
            }

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooledBuf);
        }
    }

    private ITextureLoader? FindLoader(string path)
    {
        lock (_lock)
        {
            ITextureLoader? best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < _loaders.Count; i++)
            {
                var l = _loaders[i];
                if (l.CanLoad(path) && l.Priority > bestPriority)
                {
                    best = l;
                    bestPriority = l.Priority;
                }
            }
            return best;
        }
    }

    private void TrackDevice(nint devicePtr)
    {
        lock (_lock)
        {
            if (_trackedDevices.Add(devicePtr))
            {
                DeviceRefCounts.AddOrUpdate(devicePtr, 1, (_, c) => c + 1);
            }
        }
    }

    private static void EvictDevice(nint devicePtr)
    {
        var removed = GpuTextureCache.RemoveWhere(k => k.DevicePtr == devicePtr);
        foreach (var (_, tex) in removed)
        {
            SafeDispose(tex.Texture);
        }
    }

    public void Dispose()
    {
        HashSet<nint> devicesToEvict;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            devicesToEvict = new HashSet<nint>(_trackedDevices);
            _trackedDevices.Clear();
        }

        foreach (var devicePtr in devicesToEvict)
        {
            var newCount = DeviceRefCounts.AddOrUpdate(devicePtr, 0, (_, c) => Math.Max(0, c - 1));
            if (newCount <= 0)
            {
                DeviceRefCounts.TryRemove(devicePtr, out _);
                EvictDevice(devicePtr);
            }
        }

        List<ITextureLoader> loadersCopy;
        lock (_lock)
        {
            loadersCopy = new List<ITextureLoader>(_loaders);
            _loaders.Clear();
        }

        foreach (var loader in loadersCopy)
        {
            if (loader is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }
        }
    }

}
