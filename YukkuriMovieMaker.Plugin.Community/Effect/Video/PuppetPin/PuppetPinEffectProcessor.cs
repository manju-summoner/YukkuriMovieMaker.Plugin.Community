using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    internal sealed class PuppetPinEffectProcessor(IGraphicsDevicesAndContext devices, PuppetPinEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly PuppetPinEffect item = item;

        PuppetPinCustomEffect? effect;
        ID2D1DeviceContext? deviceContext;

        PinCacheKey? lastCacheKey;
        PinGpuCache? gpuCache;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var pins = item.Pins;
            var stiffness = (float)item.Stiffness.GetValue(frame, length, fps);

            var samples = new List<PinSample>(pins.Count);
            for (var i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var rx = (float)pin.RestX.GetValue(frame, length, fps);
                var ry = (float)pin.RestY.GetValue(frame, length, fps);
                var ox = pin.IsEnabled ? (float)pin.OffsetX.GetValue(frame, length, fps) : 0f;
                var oy = pin.IsEnabled ? (float)pin.OffsetY.GetValue(frame, length, fps) : 0f;
                samples.Add(new PinSample(i, new Vector2(rx, ry), new Vector2(rx + ox, ry + oy), pin.IsEnabled));
            }

            var activeSamples = samples.FindAll(s => s.IsEnabled);
            var gpuPinCount = Math.Min(activeSamples.Count, PuppetPinCustomEffect.MaxPins);

            var inputBounds = deviceContext is not null && input is not null
                ? deviceContext.GetImageLocalBounds(input)
                : default;
            float imageWidth = inputBounds.Right - inputBounds.Left;
            float imageHeight = inputBounds.Bottom - inputBounds.Top;

            var key = PinCacheKey.Build(activeSamples, gpuPinCount, stiffness, imageWidth, imageHeight);

            if (lastCacheKey is null || !lastCacheKey.Value.Equals(key))
            {
                gpuCache?.Dispose();
                gpuCache = BuildGpuCache(activeSamples, gpuPinCount, stiffness, imageWidth, imageHeight);
                lastCacheKey = key;
            }

            var maxDisplacement = 0f;
            foreach (var s in activeSamples)
            {
                var dx = Math.Abs(s.Current.X - s.Rest.X);
                var dy = Math.Abs(s.Current.Y - s.Rest.Y);
                if (dx > maxDisplacement) maxDisplacement = dx;
                if (dy > maxDisplacement) maxDisplacement = dy;
            }

            effect.SetInput(1, gpuCache!.DataBitmap, true);
            effect.PinCount = gpuPinCount;
            effect.Stiffness = stiffness;
            effect.MaxDisplacement = maxDisplacement;

            var (tl, tt, tr, tb) = gpuCache.TightBounds;
            effect.TightLocalLeft = tl;
            effect.TightLocalTop = tt;
            effect.TightLocalRight = tr;
            effect.TightLocalBottom = tb;

            return effectDescription.DrawDescription with
            {
                Controllers = ImmutableList.CreateRange(BuildControllers(samples, activeSamples))
            };
        }

        PinGpuCache BuildGpuCache(
            List<PinSample> activeSamples,
            int gpuPinCount,
            float stiffness,
            float imageWidth,
            float imageHeight)
        {
            var maxPins = PuppetPinCustomEffect.MaxPins;
            var data = new float[maxPins * 4];

            for (var i = 0; i < gpuPinCount; i++)
            {
                var s = activeSamples[i];
                data[i * 4 + 0] = s.Rest.X;
                data[i * 4 + 1] = s.Rest.Y;
                data[i * 4 + 2] = s.Current.X;
                data[i * 4 + 3] = s.Current.Y;
            }

            ID2D1Bitmap dataBitmap;
            unsafe
            {
                fixed (float* pData = data)
                {
                    var props = new BitmapProperties1(
                        new Vortice.DCommon.PixelFormat(Format.R32G32B32A32_Float, Vortice.DCommon.AlphaMode.Premultiplied),
                        96f, 96f, BitmapOptions.None);
                    dataBitmap = deviceContext!.CreateBitmap(
                        new SizeI(maxPins, 1),
                        (nint)pData,
                        maxPins * 16,
                        props);
                }
            }

            (float left, float top, float right, float bottom) tightBounds;
            if (gpuPinCount > 0 && imageWidth > 0 && imageHeight > 0)
            {
                var restList = activeSamples.ConvertAll(s => s.Rest);
                var currentList = activeSamples.ConvertAll(s => s.Current);
                tightBounds = MlsDeformBounds.Compute(imageWidth, imageHeight, restList, currentList, stiffness);
            }
            else
            {
                var halfW = imageWidth * 0.5f;
                var halfH = imageHeight * 0.5f;
                tightBounds = (-halfW, -halfH, halfW, halfH);
            }

            return new PinGpuCache(dataBitmap, tightBounds);
        }

        List<VideoEffectController> BuildControllers(List<PinSample> samples, List<PinSample> activeSamples)
        {
            var controllers = new List<VideoEffectController>(samples.Count * 3);

            var (_, meshEdges) = DelaunayTriangulation.Compute(activeSamples.ConvertAll(s => s.Current));
            foreach (var edge in meshEdges)
            {
                var pA = activeSamples[edge.A].Current;
                var pB = activeSamples[edge.B].Current;
                controllers.Add(new VideoEffectController(item, new[]
                {
                    new ControllerPoint(new Vector3(pA.X, pA.Y, 0f)),
                    new ControllerPoint(new Vector3(pB.X, pB.Y, 0f)),
                })
                { Connection = VideoControllerPointConnection.Line });
            }

            foreach (var s in samples)
            {
                var pin = item.Pins[s.PinIndex];

                var restPoint = new ControllerPoint(
                    new Vector3(s.Rest.X, s.Rest.Y, 0f),
                    arg =>
                    {
                        if (!pin.IsRestSelected)
                            SelectRestExclusively(pin);
                        pin.RestX.AddToEachValues(arg.Delta.X);
                        pin.RestY.AddToEachValues(arg.Delta.Y);
                    })
                {
                    Shape = VideoControllerPointShape.Circle
                };
                controllers.Add(new VideoEffectController(item, new[] { restPoint }));

                if (!s.IsEnabled)
                    continue;

                controllers.Add(new VideoEffectController(item, new[]
                {
                    new ControllerPoint(new Vector3(s.Rest.X, s.Rest.Y, 0f)),
                    new ControllerPoint(new Vector3(s.Current.X, s.Current.Y, 0f)),
                })
                { Connection = VideoControllerPointConnection.Line });

                var offsetPoint = new ControllerPoint(
                    new Vector3(s.Current.X, s.Current.Y, 0f),
                    arg =>
                    {
                        if (!pin.IsOffsetSelected)
                            SelectOffsetExclusively(pin);
                        pin.OffsetX.AddToEachValues(arg.Delta.X);
                        pin.OffsetY.AddToEachValues(arg.Delta.Y);
                    })
                {
                    Shape = VideoControllerPointShape.Point
                };
                controllers.Add(new VideoEffectController(item, new[] { offsetPoint }));
            }

            return controllers;
        }

        void SelectRestExclusively(PuppetPin target)
        {
            foreach (var p in item.Pins)
            {
                p.IsOffsetSelected = false;
                p.IsRestSelected = (p == target);
            }
        }

        void SelectOffsetExclusively(PuppetPin target)
        {
            foreach (var p in item.Pins)
            {
                p.IsRestSelected = false;
                p.IsOffsetSelected = (p == target);
            }
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            deviceContext = devices.DeviceContext;
            effect = new PuppetPinCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            effect?.SetInput(1, null, true);
            gpuCache?.Dispose();
            gpuCache = null;
            lastCacheKey = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gpuCache?.Dispose();
                gpuCache = null;
                deviceContext = null;
                effect = null;
            }
            base.Dispose(disposing);
        }

        readonly struct PinSample(int pinIndex, Vector2 rest, Vector2 current, bool isEnabled)
        {
            public int PinIndex { get; } = pinIndex;
            public Vector2 Rest { get; } = rest;
            public Vector2 Current { get; } = current;
            public bool IsEnabled { get; } = isEnabled;
        }

        readonly struct PinCacheKey : IEquatable<PinCacheKey>
        {
            private readonly int pinCount;
            private readonly float stiffness;
            private readonly float imageWidth;
            private readonly float imageHeight;
            private readonly float[] pinData;

            private PinCacheKey(int pinCount, float stiffness, float imageWidth, float imageHeight, float[] pinData)
            {
                this.pinCount = pinCount;
                this.stiffness = stiffness;
                this.imageWidth = imageWidth;
                this.imageHeight = imageHeight;
                this.pinData = pinData;
            }

            public static PinCacheKey Build(
                List<PinSample> activeSamples,
                int gpuPinCount,
                float stiffness,
                float imageWidth,
                float imageHeight)
            {
                var data = new float[gpuPinCount * 4];
                for (var i = 0; i < gpuPinCount; i++)
                {
                    var s = activeSamples[i];
                    data[i * 4 + 0] = s.Rest.X;
                    data[i * 4 + 1] = s.Rest.Y;
                    data[i * 4 + 2] = s.Current.X;
                    data[i * 4 + 3] = s.Current.Y;
                }
                return new PinCacheKey(gpuPinCount, stiffness, imageWidth, imageHeight, data);
            }

            public bool Equals(PinCacheKey other)
            {
                if (pinCount != other.pinCount) return false;
                if (stiffness != other.stiffness) return false;
                if (imageWidth != other.imageWidth) return false;
                if (imageHeight != other.imageHeight) return false;
                return pinData.AsSpan().SequenceEqual(other.pinData.AsSpan());
            }

            public override bool Equals(object? obj) => obj is PinCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(pinCount);
                hash.Add(stiffness);
                hash.Add(imageWidth);
                hash.Add(imageHeight);
                foreach (var v in pinData)
                    hash.Add(v);
                return hash.ToHashCode();
            }
        }

        sealed class PinGpuCache(
            ID2D1Bitmap dataBitmap,
            (float Left, float Top, float Right, float Bottom) tightBounds) : IDisposable
        {
            public ID2D1Bitmap DataBitmap { get; } = dataBitmap;
            public (float Left, float Top, float Right, float Bottom) TightBounds { get; } = tightBounds;

            public void Dispose() => DataBitmap.Dispose();
        }
    }
}
