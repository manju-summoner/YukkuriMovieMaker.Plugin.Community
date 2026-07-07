using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldWarpPreviewRenderer : IDisposable
    {
        readonly GraphicsDevices devices;
        readonly IGraphicsDevicesAndContext context;
        readonly VectorFieldWarpCustomEffect effect;
        readonly D3DImage d3dImage = new();

        IntPtr d3d9;
        IntPtr d3d9Device;
        IntPtr d3d9Texture;
        IntPtr d3d9Surface;

        ID2D1Bitmap1? inputBitmap;
        ID3D11Texture2D? sharedTexture;
        ID2D1Bitmap1? targetBitmap;
        int margin;
        bool disposedValue;

        public bool IsEnabled { get; }

        public ImageSource ImageSource => d3dImage;

        public int OutputWidth { get; private set; }

        public int OutputHeight { get; private set; }

        public event EventHandler? RedrawRequested;

        public VectorFieldWarpPreviewRenderer()
        {
            devices = new GraphicsDevices();
            context = devices.CreateContext();
            effect = new VectorFieldWarpCustomEffect(context);
            IsEnabled = effect.IsEnabled && TryCreateD3D9Device();
            d3dImage.IsFrontBufferAvailableChanged += D3DImage_IsFrontBufferAvailableChanged;
        }

        void D3DImage_IsFrontBufferAvailableChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (d3dImage.IsFrontBufferAvailable)
                RedrawRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetSource(byte[] pixels, int width, int height, int sourceMargin)
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);

            effect.SetInput(0, null, true);
            ReleaseSurfaces();

            margin = sourceMargin;
            OutputWidth = width + margin * 2;
            OutputHeight = height + margin * 2;

            var deviceContext = context.DeviceContext;
            var pixelFormat = new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);

            var inputProps = new BitmapProperties1(pixelFormat, 96, 96, BitmapOptions.None);
            inputBitmap = deviceContext.CreateBitmap(new SizeI(width, height), inputProps);
            inputBitmap.CopyFromMemory(pixels, width * 4);
            effect.SetInput(0, inputBitmap, true);

            sharedTexture = devices.D3D.Device.CreateTexture2D(new Texture2DDescription
            {
                Width = OutputWidth,
                Height = OutputHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.Shared,
            });
            using (var dxgiSurface = sharedTexture.QueryInterface<IDXGISurface>())
            {
                var targetProps = new BitmapProperties1(pixelFormat, 96, 96, BitmapOptions.Target | BitmapOptions.CannotDraw);
                targetBitmap = deviceContext.CreateBitmapFromDxgiSurface(dxgiSurface, targetProps);
            }

            IntPtr sharedHandle;
            using (var dxgiResource = sharedTexture.QueryInterface<IDXGIResource>())
                sharedHandle = dxgiResource.SharedHandle;
            CreateD3D9SharedTexture(sharedHandle);

            d3dImage.Lock();
            d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, d3d9Surface, true);
            d3dImage.Unlock();
        }

        static readonly Duration LockTimeout = new(TimeSpan.FromMilliseconds(1));

        public bool Render(byte[] pointData, int pointCount, float amount, float maxDisplacement, int integrationSteps)
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (inputBitmap is null || targetBitmap is null || d3d9Surface == IntPtr.Zero)
                throw new InvalidOperationException();

            if (!d3dImage.TryLock(LockTimeout))
            {
                d3dImage.Unlock();
                return false;
            }
            try
            {
                effect.PointData = pointData;
                effect.PointCount = pointCount;
                effect.Amount = amount;
                effect.MaxDisplacement = maxDisplacement;
                effect.IntegrationSteps = integrationSteps;

                var deviceContext = context.DeviceContext;
                using var output = effect.Output;
                deviceContext.Target = targetBitmap;
                deviceContext.BeginDraw();
                deviceContext.Clear(new Color4(0f, 0f, 0f, 0f));
                deviceContext.DrawImage(
                    output,
                    new Vector2(0f, 0f),
                    new Vortice.Mathematics.Rect(-margin, -margin, OutputWidth, OutputHeight),
                    InterpolationMode.Linear,
                    CompositeMode.SourceCopy);
                deviceContext.EndDraw();
                deviceContext.Target = null;
                devices.D3D.DeviceContext.Flush();

                d3dImage.AddDirtyRect(new Int32Rect(0, 0, OutputWidth, OutputHeight));
            }
            finally
            {
                d3dImage.Unlock();
            }
            return true;
        }

        unsafe bool TryCreateD3D9Device()
        {
            if (NativeMethods.Direct3DCreate9Ex(NativeMethods.SdkVersion, out d3d9) < 0 || d3d9 == IntPtr.Zero)
                return false;

            var presentParameters = new NativeMethods.PresentParameters
            {
                BackBufferWidth = 1,
                BackBufferHeight = 1,
                BackBufferFormat = 0,
                BackBufferCount = 1,
                SwapEffect = 1,
                DeviceWindow = NativeMethods.GetDesktopWindow(),
                Windowed = 1,
                PresentationInterval = 0,
            };

            var vtbl = *(void***)d3d9;
            var createDeviceEx = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, uint, NativeMethods.PresentParameters*, IntPtr, IntPtr*, int>)vtbl[20];
            IntPtr device;
            var hr = createDeviceEx(
                d3d9,
                0,
                NativeMethods.DeviceTypeHardware,
                presentParameters.DeviceWindow,
                NativeMethods.CreateHardwareVertexProcessing | NativeMethods.CreateMultithreaded | NativeMethods.CreateFpuPreserve,
                &presentParameters,
                IntPtr.Zero,
                &device);
            if (hr < 0)
                return false;
            d3d9Device = device;
            return true;
        }

        unsafe void CreateD3D9SharedTexture(IntPtr sharedHandle)
        {
            var vtbl = *(void***)d3d9Device;
            var createTexture = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, uint, uint, uint, IntPtr*, IntPtr*, int>)vtbl[23];
            IntPtr texture;
            var handle = sharedHandle;
            var hr = createTexture(
                d3d9Device,
                (uint)OutputWidth,
                (uint)OutputHeight,
                1,
                NativeMethods.UsageRenderTarget,
                NativeMethods.FormatA8R8G8B8,
                NativeMethods.PoolDefault,
                &texture,
                &handle);
            if (hr < 0)
                throw new InvalidOperationException();
            d3d9Texture = texture;

            var textureVtbl = *(void***)d3d9Texture;
            var getSurfaceLevel = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)textureVtbl[18];
            IntPtr surface;
            hr = getSurfaceLevel(d3d9Texture, 0, &surface);
            if (hr < 0)
                throw new InvalidOperationException();
            d3d9Surface = surface;
        }

        void ReleaseSurfaces()
        {
            if (d3d9Surface != IntPtr.Zero)
            {
                d3dImage.Lock();
                d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                d3dImage.Unlock();
            }
            NativeMethods.Release(ref d3d9Surface);
            NativeMethods.Release(ref d3d9Texture);
            targetBitmap?.Dispose();
            targetBitmap = null;
            sharedTexture?.Dispose();
            sharedTexture = null;
            inputBitmap?.Dispose();
            inputBitmap = null;
        }

        public void Dispose()
        {
            if (disposedValue)
                return;
            d3dImage.IsFrontBufferAvailableChanged -= D3DImage_IsFrontBufferAvailableChanged;
            if (IsEnabled)
                effect.SetInput(0, null, true);
            ReleaseSurfaces();
            NativeMethods.Release(ref d3d9Device);
            NativeMethods.Release(ref d3d9);
            effect.Dispose();
            context.Dispose();
            devices.Dispose();
            disposedValue = true;
        }

        static class NativeMethods
        {
            public const uint SdkVersion = 32;
            public const uint DeviceTypeHardware = 1;
            public const uint CreateFpuPreserve = 0x2;
            public const uint CreateMultithreaded = 0x4;
            public const uint CreateHardwareVertexProcessing = 0x40;
            public const uint UsageRenderTarget = 0x1;
            public const uint FormatA8R8G8B8 = 21;
            public const uint PoolDefault = 0;

            [DllImport("d3d9.dll")]
            public static extern int Direct3DCreate9Ex(uint sdkVersion, out IntPtr d3d);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDesktopWindow();

            public static unsafe void Release(ref IntPtr comObject)
            {
                if (comObject == IntPtr.Zero)
                    return;
                var vtbl = *(void***)comObject;
                var release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)vtbl[2];
                release(comObject);
                comObject = IntPtr.Zero;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PresentParameters
            {
                public uint BackBufferWidth;
                public uint BackBufferHeight;
                public uint BackBufferFormat;
                public uint BackBufferCount;
                public uint MultiSampleType;
                public uint MultiSampleQuality;
                public uint SwapEffect;
                public IntPtr DeviceWindow;
                public int Windowed;
                public int EnableAutoDepthStencil;
                public uint AutoDepthStencilFormat;
                public uint Flags;
                public uint FullScreenRefreshRateInHz;
                public uint PresentationInterval;
            }
        }
    }
}
