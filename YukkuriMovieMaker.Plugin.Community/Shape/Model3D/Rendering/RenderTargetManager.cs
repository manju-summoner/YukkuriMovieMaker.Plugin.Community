using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using static YukkuriMovieMaker.Plugin.Community.Shape.Model3D.DisposeUtility;
using D2D = Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal sealed class RenderTargetManager : IDisposable
{
    private readonly Lock _lock = new();

    private ID3D11Texture2D? _renderTargetTexture;
    private ID3D11RenderTargetView? _renderTargetView;
    private ID3D11Texture2D? _depthStencilTexture;
    private ID3D11DepthStencilView? _depthStencilView;
    private D2D.ID2D1Bitmap1? _sharedBitmap;

    private int _width;
    private int _height;
    private bool _disposed;

    public ID3D11RenderTargetView? RenderTargetView => _renderTargetView;
    public ID3D11DepthStencilView? DepthStencilView => _depthStencilView;
    public D2D.ID2D1Bitmap1? SharedBitmap => _sharedBitmap;

    public bool EnsureSize(IGraphicsDevicesAndContext devices, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (width < 1 || height < 1) return false;

        lock (_lock)
        {
            if (_disposed) return false;
            if (_renderTargetView is not null && _width == width && _height == height) return true;

            DisposeResources();

            try
            {
                var device = devices.D3D.Device;

                _renderTargetTexture = CreateRenderTargetTexture(device, width, height);
                _renderTargetView = device.CreateRenderTargetView(_renderTargetTexture);
                _depthStencilTexture = CreateDepthStencilTexture(device, width, height);
                _depthStencilView = CreateDepthStencilView(device, _depthStencilTexture);
                _sharedBitmap = CreateSharedBitmap(devices, _renderTargetTexture);

                _width = width;
                _height = height;
                return true;
            }
            catch
            {
                DisposeResources();
                _width = 0;
                _height = 0;
                return false;
            }
        }
    }

    private static ID3D11Texture2D CreateRenderTargetTexture(ID3D11Device device, int width, int height)
        => device.CreateTexture2D(new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });

    private static ID3D11Texture2D CreateDepthStencilTexture(ID3D11Device device, int width, int height)
        => device.CreateTexture2D(new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R24G8_Typeless,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });

    private static ID3D11DepthStencilView CreateDepthStencilView(ID3D11Device device, ID3D11Texture2D texture)
        => device.CreateDepthStencilView(texture, new DepthStencilViewDescription
        {
            Format = Format.D24_UNorm_S8_UInt,
            ViewDimension = DepthStencilViewDimension.Texture2D,
            Texture2D = new Texture2DDepthStencilView { MipSlice = 0 }
        });

    private static D2D.ID2D1Bitmap1 CreateSharedBitmap(IGraphicsDevicesAndContext devices, ID3D11Texture2D renderTargetTexture)
    {
        using var surface = renderTargetTexture.QueryInterface<IDXGISurface>();
        var properties = new D2D.BitmapProperties1(
            new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96,
            96,
            D2D.BitmapOptions.Target);

        return devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, properties);
    }

    private void DisposeResources()
    {
        SafeDispose(ref _sharedBitmap);
        SafeDispose(ref _depthStencilView);
        SafeDispose(ref _depthStencilTexture);
        SafeDispose(ref _renderTargetView);
        SafeDispose(ref _renderTargetTexture);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            DisposeResources();
        }
    }
}
