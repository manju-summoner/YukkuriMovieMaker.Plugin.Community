using ComputeWeave;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    internal sealed class DirectionalColorKeyInteropProvider : ComputeExternalDirect3D11Provider
    {
        private readonly ID2D1DeviceContext6 renderContext;

        private DirectionalColorKeyInteropProvider(
            ID3D11Device1 device,
            ID3D11DeviceContext4 context,
            ID2D1DeviceContext6 renderContext,
            ComputeExternalQueueScheduler scheduler)
            : base(device.NativePointer, context.NativePointer, renderContext.NativePointer, scheduler)
        {
            this.renderContext = renderContext;
        }

        public ID2D1DeviceContext6 RenderContext => renderContext;

        public static DirectionalColorKeyInteropProvider? TryCreate(
            IGraphicsDevicesAndContext devices,
            ComputeExternalQueueScheduler scheduler,
            out GraphicsDevice? graphicsDevice)
        {
            ArgumentNullException.ThrowIfNull(scheduler);

            graphicsDevice = null;

            ID3D11Device1? device = null;
            ID3D11DeviceContext4? context = null;
            ID2D1DeviceContext6? renderContext = null;

            try
            {
                if (!GraphicsDevice.TryGetDevice(new ExternalAdapterIdentity(devices.DXGI.Adapter.Description.Luid), out graphicsDevice))
                    return null;

                device = devices.D3D.Device.QueryInterface<ID3D11Device1>();
                context = devices.D3D.DeviceContext.QueryInterface<ID3D11DeviceContext4>();
                renderContext = devices.D2D.Device.CreateDeviceContext(DeviceContextOptions.EnableMultithreadedOptimizations)
                    .QueryInterface<ID2D1DeviceContext6>();

                var provider = new DirectionalColorKeyInteropProvider(device, context, renderContext, scheduler);

                renderContext = null;

                return provider;
            }
            catch
            {
                graphicsDevice = null;
                return null;
            }
            finally
            {
                renderContext?.Dispose();
                context?.Dispose();
                device?.Dispose();
            }
        }

        protected override void DisposeCore()
        {
            renderContext.Dispose();
        }
    }

    [ComputePipelineHost("device", 2)]
    internal sealed partial class DirectionalColorKeyInteropHost
    {
        private readonly GraphicsDevice device;

        [ComputePipeline]
        [ComputeInterop]
        private void CaptureSource(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> source,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> bgra,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new SharedTextureToBufferShader(source, bgra, width, height));
        }

        [ComputePipeline]
        [ComputeInterop]
        private void WriteForegroundField(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> destination,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<int> foreground,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new BufferToSharedTextureShader(foreground, destination, width, height));
        }
    }

    [ComputeInteropResourceSet]
    internal sealed partial class DirectionalColorKeyResourceSet
    {
        [ComputeSharedTexture(
            ComputeResourceResizePolicy.Exact,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost)]
        private readonly SharedTextureSlot<Bgra32, Float4, ExternalDirect3D11TextureView> source;

        [ComputeSharedTexture(
            ComputeResourceResizePolicy.Exact,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Read,
            ExternalTextureUsage.Sampled,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.Compute,
            ComputeResourceRecovery.Recompute)]
        private readonly SharedTextureSlot<Bgra32, Float4, ExternalDirect3D11TextureView> foreground;
    }
}
