using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
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

        ID2D1Bitmap1? inputBitmap;
        ID2D1Bitmap1? targetBitmap;
        ID2D1Bitmap1? readBitmap;
        byte[] outputBuffer = [];
        int margin;
        bool disposedValue;

        public bool IsEnabled { get; }

        public int OutputWidth { get; private set; }
        public int OutputHeight { get; private set; }

        public VectorFieldWarpPreviewRenderer()
        {
            devices = new GraphicsDevices();
            context = devices.CreateContext();
            effect = new VectorFieldWarpCustomEffect(context);
            IsEnabled = effect.IsEnabled;
        }

        public void SetSource(byte[] pixels, int width, int height, int sourceMargin)
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);

            effect.SetInput(0, null, true);
            ReleaseBitmaps();

            margin = sourceMargin;
            OutputWidth = width + margin * 2;
            OutputHeight = height + margin * 2;
            outputBuffer = new byte[OutputWidth * OutputHeight * 4];

            var deviceContext = context.DeviceContext;
            var pixelFormat = new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var inputProps = new BitmapProperties1(pixelFormat, 96, 96, BitmapOptions.None);
            var targetProps = new BitmapProperties1(pixelFormat, 96, 96, BitmapOptions.Target);
            var readProps = new BitmapProperties1(pixelFormat, 96, 96, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);

            inputBitmap = deviceContext.CreateBitmap(new SizeI(width, height), inputProps);
            inputBitmap.CopyFromMemory(pixels, width * 4);
            targetBitmap = deviceContext.CreateBitmap(new SizeI(OutputWidth, OutputHeight), targetProps);
            readBitmap = deviceContext.CreateBitmap(new SizeI(OutputWidth, OutputHeight), readProps);

            effect.SetInput(0, inputBitmap, true);
        }

        public byte[] Render(byte[] pointData, int pointCount, float amount, float maxDisplacement, int integrationSteps)
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (inputBitmap is null || targetBitmap is null || readBitmap is null)
                throw new InvalidOperationException();

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
                new Rect(-margin, -margin, OutputWidth, OutputHeight),
                InterpolationMode.Linear,
                CompositeMode.SourceCopy);
            deviceContext.EndDraw();
            deviceContext.Target = null;

            readBitmap.CopyFromBitmap(targetBitmap);
            var map = readBitmap.Map(MapOptions.Read);
            try
            {
                var rowBytes = OutputWidth * 4;
                for (var y = 0; y < OutputHeight; y++)
                    Marshal.Copy(map.Bits + (nint)y * map.Pitch, outputBuffer, y * rowBytes, rowBytes);
            }
            finally
            {
                readBitmap.Unmap();
            }
            return outputBuffer;
        }

        void ReleaseBitmaps()
        {
            inputBitmap?.Dispose();
            inputBitmap = null;
            targetBitmap?.Dispose();
            targetBitmap = null;
            readBitmap?.Dispose();
            readBitmap = null;
        }

        public void Dispose()
        {
            if (disposedValue)
                return;
            if (IsEnabled)
                effect.SetInput(0, null, true);
            ReleaseBitmaps();
            effect.Dispose();
            context.Dispose();
            devices.Dispose();
            disposedValue = true;
        }
    }
}
