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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationEffectProcessor(IGraphicsDevicesAndContext devices, PuppetDeformationEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly PuppetDeformationEffect item = item;
        readonly float[] pinDataBuffer = new float[PuppetDeformationCustomEffect.MaxPins * 4];

        PuppetDeformationCustomEffect? effect;
        ID2D1DeviceContext? deviceContext;
        PinGpuCache? gpuCache;
        ImmutableList<VideoEffectController> cachedControllers = ImmutableList<VideoEffectController>.Empty;

        bool isFirst = true;
        int pinCount;
        float stiffness;
        float imageWidth;
        float imageHeight;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var pins = item.Pins;
            var stiffness = (float)item.Stiffness.GetValue(frame, length, fps);

            var pinCount = Math.Min(pins.Count, PuppetDeformationCustomEffect.MaxPins);
            var samples = new List<PinSample>(pinCount);
            for (var i = 0; i < pinCount; i++)
            {
                var pin = pins[i];
                var rx = (float)pin.RestX.GetValue(frame, length, fps);
                var ry = (float)pin.RestY.GetValue(frame, length, fps);
                var ox = pin.IsEnabled ? (float)pin.OffsetX.GetValue(frame, length, fps) : 0f;
                var oy = pin.IsEnabled ? (float)pin.OffsetY.GetValue(frame, length, fps) : 0f;
                samples.Add(new PinSample(i, new Vector2(rx, ry), new Vector2(rx + ox, ry + oy), pin.IsEnabled));
            }

            var inputBounds = deviceContext is not null && input is not null
                ? deviceContext.GetImageLocalBounds(input)
                : default;
            var imageWidth = inputBounds.Right - inputBounds.Left;
            var imageHeight = inputBounds.Bottom - inputBounds.Top;

            if (isFirst
                || this.pinCount != pinCount
                || this.stiffness != stiffness
                || this.imageWidth != imageWidth
                || this.imageHeight != imageHeight
                || !PinSamplesMatchBuffer(samples, pinCount))
            {
                gpuCache?.Dispose();
                gpuCache = BuildGpuCache(pinCount, stiffness, imageWidth, imageHeight, samples);

                effect.SetInput(1, gpuCache.DataBitmap, true);
                effect.PinCount = pinCount;
                effect.Stiffness = stiffness;

                var (tl, tt, tr, tb) = gpuCache.TightBounds;
                effect.TightLocalLeft = tl;
                effect.TightLocalTop = tt;
                effect.TightLocalRight = tr;
                effect.TightLocalBottom = tb;

                cachedControllers = ImmutableList.CreateRange(BuildControllers(samples));
            }

            isFirst = false;
            this.pinCount = pinCount;
            this.stiffness = stiffness;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;

            return effectDescription.DrawDescription with
            {
                Controllers = cachedControllers
            };
        }

        bool PinSamplesMatchBuffer(List<PinSample> samples, int pinCount)
        {
            for (var i = 0; i < pinCount; i++)
            {
                var s = samples[i];
                if (pinDataBuffer[i * 4 + 0] != s.Rest.X) return false;
                if (pinDataBuffer[i * 4 + 1] != s.Rest.Y) return false;
                if (pinDataBuffer[i * 4 + 2] != s.Current.X) return false;
                if (pinDataBuffer[i * 4 + 3] != s.Current.Y) return false;
            }
            return true;
        }

        PinGpuCache BuildGpuCache(
            int pinCount,
            float stiffness,
            float imageWidth,
            float imageHeight,
            List<PinSample> samples)
        {
            var maxPins = PuppetDeformationCustomEffect.MaxPins;

            for (var i = 0; i < pinCount; i++)
            {
                var s = samples[i];
                pinDataBuffer[i * 4 + 0] = s.Rest.X;
                pinDataBuffer[i * 4 + 1] = s.Rest.Y;
                pinDataBuffer[i * 4 + 2] = s.Current.X;
                pinDataBuffer[i * 4 + 3] = s.Current.Y;
            }
            Array.Clear(pinDataBuffer, pinCount * 4, (maxPins - pinCount) * 4);

            ID2D1Bitmap dataBitmap;
            unsafe
            {
                fixed (float* pData = pinDataBuffer)
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
            if (pinCount > 0 && imageWidth > 0 && imageHeight > 0)
            {
                var restList = new SampleProjection(samples, pinCount, static s => s.Rest);
                var currentList = new SampleProjection(samples, pinCount, static s => s.Current);
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

        List<VideoEffectController> BuildControllers(List<PinSample> samples)
        {
            var controllers = new List<VideoEffectController>(samples.Count * 3);

            var (_, meshEdges) = DelaunayTriangulation.Compute(samples.ConvertAll(s => s.Current));
            foreach (var edge in meshEdges)
            {
                var pA = samples[edge.A].Current;
                var pB = samples[edge.B].Current;
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

        void SelectRestExclusively(PuppetDeformation target)
        {
            foreach (var p in item.Pins)
            {
                p.IsOffsetSelected = false;
                p.IsRestSelected = (p == target);
            }
        }

        void SelectOffsetExclusively(PuppetDeformation target)
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
            effect = new PuppetDeformationCustomEffect(devices);
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
            cachedControllers = ImmutableList<VideoEffectController>.Empty;
            isFirst = true;
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

        readonly struct SampleProjection(List<PinSample> samples, int count, Func<PinSample, Vector2> selector)
            : IReadOnlyList<Vector2>
        {
            public Vector2 this[int index] => selector(samples[index]);
            public int Count => count;
            public IEnumerator<Vector2> GetEnumerator()
            {
                for (var i = 0; i < count; i++)
                    yield return selector(samples[i]);
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
