using System;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;
using AlphaMode = Vortice.DCommon.AlphaMode;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeSurface : IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly ID3D11UnorderedAccessView? uav;
        bool disposed;

        public int Width { get; }
        public int Height { get; }
        public ID2D1Bitmap1 Bitmap { get; }
        public ID3D11ShaderResourceView Srv { get; }
        public ID3D11UnorderedAccessView Uav
            => uav ?? throw new InvalidOperationException("書き込み不可の面に順不同アクセスビューはありません。");

        public ComputeSurface(ComputeShaderDevice device, ID2D1DeviceContext dc, int width, int height, bool writable)
        {
            Width = width;
            Height = height;

            var bindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource;
            if (writable)
                bindFlags |= BindFlags.UnorderedAccess;

            var texture = device.Device.CreateTexture2D(new Texture2DDescription(
                Format.B8G8R8A8_UNorm,
                width,
                height,
                1,
                1,
                bindFlags,
                ResourceUsage.Default,
                CpuAccessFlags.None,
                1,
                0,
                ResourceOptionFlags.None), null);
            disposer.Collect(texture);

            Srv = device.Device.CreateShaderResourceView(texture, null);
            disposer.Collect(Srv);

            if (writable)
            {
                uav = device.Device.CreateUnorderedAccessView(texture, null);
                disposer.Collect(uav);
            }

            using var surface = texture.QueryInterface<IDXGISurface>();
            Bitmap = dc.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.Target));
            disposer.Collect(Bitmap);
        }

        public static bool IsWritableFormatSupported(ComputeShaderDevice device)
            => device.Device.CheckFormatSupport(Format.B8G8R8A8_UNorm).HasFlag(FormatSupport.TypedUnorderedAccessView);

        public void Dispose()
        {
            if (disposed)
                return;

            disposer.DisposeAndClear();
            disposed = true;
        }
    }
}
