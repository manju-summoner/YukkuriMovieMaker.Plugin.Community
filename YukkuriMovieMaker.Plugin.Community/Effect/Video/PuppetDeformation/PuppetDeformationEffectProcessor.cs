using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Windows.Input;
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
        bool apply = true;
        int pinCount;
        float stiffness;
        float imageWidth;
        float imageHeight;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (!PuppetDeformationFrameService.IsInternalRendering)
            {
                var scene = effectDescription.Scenes.FirstOrDefault(x => x.ID == effectDescription.SceneId);
                if (scene != null)
                {
                    PuppetDeformationFrameService.Publish(scene, effectDescription.TimelinePosition.Frame, effectDescription.FPS, effectDescription.ItemPosition.Frame, effectDescription.ItemDuration.Frame);
                }
            }

            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var pins = item.Pins;
            var stiffness = (float)item.Stiffness.GetValue(frame, length, fps);
            var apply = item.ApplyDeformation;

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
                || this.apply != apply
                || !PinSamplesMatchBuffer(samples))
            {
                gpuCache = BuildGpuCache(stiffness, imageWidth, imageHeight, samples);

                effect.PinData = gpuCache.PinData;
                //変形オフ時はPinCount=0を送り、シェーダー側で変形せず入力をそのまま出力する。
                effect.PinCount = apply ? pinCount : 0;
                effect.Stiffness = stiffness;

                if (apply)
                {
                    var (tl, tt, tr, tb) = gpuCache.TightBounds;
                    effect.TightLocalLeft = tl;
                    effect.TightLocalTop = tt;
                    effect.TightLocalRight = tr;
                    effect.TightLocalBottom = tb;
                }
                else
                {
                    //変形しないので出力範囲は拡張しない(入力範囲のまま)。
                    effect.TightLocalLeft = 0;
                    effect.TightLocalTop = 0;
                    effect.TightLocalRight = 0;
                    effect.TightLocalBottom = 0;
                }

                cachedControllers = [.. BuildControllers(samples)];
            }

            isFirst = false;
            this.pinCount = pinCount;
            this.stiffness = stiffness;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
            this.apply = apply;

            return effectDescription.DrawDescription with
            {
                Controllers = cachedControllers
            };
        }

        bool PinSamplesMatchBuffer(List<PinSample> samples)
        {
            for (var i = 0; i < samples.Count; i++)
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
            float stiffness,
            float imageWidth,
            float imageHeight,
            List<PinSample> samples)
        {
            var maxPins = PuppetDeformationCustomEffect.MaxPins;
            var count = samples.Count;

            var restPositions = new Vector2[count];
            var currentPositions = new Vector2[count];

            for (var i = 0; i < count; i++)
            {
                var s = samples[i];
                pinDataBuffer[i * 4 + 0] = s.Rest.X;
                pinDataBuffer[i * 4 + 1] = s.Rest.Y;
                pinDataBuffer[i * 4 + 2] = s.Current.X;
                pinDataBuffer[i * 4 + 3] = s.Current.Y;
                restPositions[i] = s.Rest;
                currentPositions[i] = s.Current;
            }
            Array.Clear(pinDataBuffer, count * 4, (maxPins - count) * 4);

            var pinData = new byte[maxPins * 16];
            Buffer.BlockCopy(pinDataBuffer, 0, pinData, 0, pinData.Length);

            (float left, float top, float right, float bottom) tightBounds;
            if (count > 0 && imageWidth > 0 && imageHeight > 0)
            {
                tightBounds = MlsDeformBounds.Compute(imageWidth, imageHeight, restPositions, currentPositions, stiffness);
            }
            else
            {
                var halfW = imageWidth * 0.5f;
                var halfH = imageHeight * 0.5f;
                tightBounds = (-halfW, -halfH, halfW, halfH);
            }

            return new PinGpuCache(pinData, tightBounds);
        }

        List<VideoEffectController> BuildControllers(List<PinSample> samples)
        {
            var controllers = new List<VideoEffectController>(samples.Count * 5);

            var (_, meshEdges) = DelaunayTriangulation.Compute(samples.ConvertAll(s => s.Current));
            foreach (var edge in meshEdges)
            {
                var pA = samples[edge.A].Current;
                var pB = samples[edge.B].Current;
                controllers.Add(new VideoEffectController(item, [
                    new ControllerPoint(new Vector3(pA.X, pA.Y, 0f)),
                    new ControllerPoint(new Vector3(pB.X, pB.Y, 0f)),
                ])
                { Connection = VideoControllerPointConnection.Line });
            }

            foreach (var s in samples)
            {
                var pin = item.Pins[s.PinIndex];

                var restPoint = new ControllerPoint(
                    new Vector3(s.Rest.X, s.Rest.Y, 0f),
                    arg =>
                    {
                        if (!pin.IsRestSelected) return;
                        ApplyRestDelta(arg.Delta.X, arg.Delta.Y);
                    })
                {
                    OnDragStart = arg =>
                    {
                        if (arg.ModifierKeys.HasFlag(ModifierKeys.Control))
                            SelectRestToggle(pin);
                        else if (!pin.IsRestSelected)
                            SelectRestExclusively(pin);
                    },
                    IsSelected = pin.IsRestSelected,
                    Shape = VideoControllerPointShape.Circle
                };
                controllers.Add(new VideoEffectController(item, [restPoint]));

                if (!s.IsEnabled)
                    continue;

                controllers.Add(new VideoEffectController(item, [
                    new ControllerPoint(new Vector3(s.Rest.X, s.Rest.Y, 0f)),
                    new ControllerPoint(new Vector3(s.Current.X, s.Current.Y, 0f)),
                ])
                { Connection = VideoControllerPointConnection.Line });

                var offsetPoint = new ControllerPoint(
                    new Vector3(s.Current.X, s.Current.Y, 0f),
                    arg =>
                    {
                        if (!pin.IsOffsetSelected) return;
                        ApplyOffsetDelta(pin, arg.Delta.X, arg.Delta.Y);
                    })
                {
                    OnDragStart = arg =>
                    {
                        if (arg.ModifierKeys.HasFlag(ModifierKeys.Control))
                            SelectOffsetToggle(pin);
                        else if (!pin.IsOffsetSelected)
                            SelectOffsetExclusively(pin);
                    },
                    IsSelected = pin.IsOffsetSelected,
                    Shape = VideoControllerPointShape.Point
                };
                controllers.Add(new VideoEffectController(item, [offsetPoint]));
            }

            return controllers;
        }

        void ApplyRestDelta(double deltaX, double deltaY)
        {
            foreach (var p in item.Pins)
            {
                if (!p.IsRestSelected) continue;
                p.RestX.AddToEachValues(deltaX);
                p.RestY.AddToEachValues(deltaY);
            }
        }

        void SelectRestToggle(PuppetDeformation pin)
        {
            if (!pin.IsRestSelected)
                pin.IsRestSelected = true;
            else if (item.Pins.Any(p => p != pin && p.IsRestSelected))
                pin.IsRestSelected = false;
        }

        void SelectRestExclusively(PuppetDeformation target)
        {
            foreach (var p in item.Pins)
            {
                p.IsOffsetSelected = false;
                p.IsRestSelected = (p == target);
            }
        }

        void ApplyOffsetDelta(PuppetDeformation source, double deltaX, double deltaY)
        {
            var syncMode = item.SyncMode;
            var selectedPins = item.Pins.Where(p => p.IsOffsetSelected).ToList();

            if (syncMode == PuppetDeformationEditorPointsSync.None || selectedPins.Count <= 1)
            {
                source.OffsetX.AddToEachValues(deltaX);
                source.OffsetY.AddToEachValues(deltaY);
                return;
            }

            var sourceRest = new Vector2(
                (float)(source.RestX.Values.FirstOrDefault()?.Value ?? 0),
                (float)(source.RestY.Values.FirstOrDefault()?.Value ?? 0));

            var maxDistance = 1f;
            if (syncMode == PuppetDeformationEditorPointsSync.Distance)
            {
                var minX = selectedPins.Min(p => (float)(p.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var maxX = selectedPins.Max(p => (float)(p.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var minY = selectedPins.Min(p => (float)(p.RestY.Values.FirstOrDefault()?.Value ?? 0));
                var maxY = selectedPins.Max(p => (float)(p.RestY.Values.FirstOrDefault()?.Value ?? 0));
                Vector2[] corners = [new(minX, minY), new(maxX, minY), new(minX, maxY), new(maxX, maxY)];
                maxDistance = corners.Max(c => Vector2.Distance(c, sourceRest)) + 1f;
            }

            foreach (var p in selectedPins)
            {
                var ratio = 1f;
                if (syncMode == PuppetDeformationEditorPointsSync.Distance)
                {
                    var px = (float)(p.RestX.Values.FirstOrDefault()?.Value ?? 0);
                    var py = (float)(p.RestY.Values.FirstOrDefault()?.Value ?? 0);
                    ratio = Math.Max(0f, 1f - Vector2.Distance(new Vector2(px, py), sourceRest) / maxDistance);
                }
                p.OffsetX.AddToEachValues(deltaX * ratio);
                p.OffsetY.AddToEachValues(deltaY * ratio);
            }
        }

        void SelectOffsetToggle(PuppetDeformation pin)
        {
            if (!pin.IsOffsetSelected)
                pin.IsOffsetSelected = true;
            else if (item.Pins.Any(p => p != pin && p.IsOffsetSelected))
                pin.IsOffsetSelected = false;
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
            gpuCache = null;
            cachedControllers = ImmutableList<VideoEffectController>.Empty;
            isFirst = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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

        sealed class PinGpuCache(
            byte[] pinData,
            (float Left, float Top, float Right, float Bottom) tightBounds)
        {
            public byte[] PinData { get; } = pinData;
            public (float Left, float Top, float Right, float Bottom) TightBounds { get; } = tightBounds;
        }
    }
}
