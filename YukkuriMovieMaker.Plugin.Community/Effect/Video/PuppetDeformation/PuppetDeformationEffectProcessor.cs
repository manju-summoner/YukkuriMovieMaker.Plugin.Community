using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationEffectProcessor(IGraphicsDevicesAndContext devices, PuppetDeformationEffect item) : VideoEffectProcessorBase(devices)
    {
        const int ArapIterations = 6;
        const float ArapMinSpacing = 8f;
        //アルファ読み戻しを行う入力サイズの上限（これを超える場合は切り離しをスキップ）
        const int ArapAlphaReadbackMaxSize = 4096;

        readonly PuppetDeformationEffect item = item;
        readonly float[] pinDataBuffer = new float[PuppetDeformationCustomEffect.MaxPins * 4];

        PuppetDeformationCustomEffect? effect;
        PuppetDeformationArapCustomEffect? arapEffect;
        ID2D1DeviceContext? deviceContext;
        PinGpuCache? gpuCache;
        ImmutableList<VideoEffectController> cachedControllers = ImmutableList<VideoEffectController>.Empty;

        //ARAP用キャッシュ
        ArapGridMesh? arapMesh;
        ArapDeformer? arapDeformer;
        Vector2[]? arapRests;
        Vector2[]? deformedPositions;
        byte[]? arapVertexData;
        bool useArapWiring;

        bool isFirst = true;
        bool apply = true;
        PuppetDeformationAlgorithm algorithm = PuppetDeformationAlgorithm.Mls;
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
            var apply = item.ApplyDeformation;
            var algorithm = item.Algorithm;

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

            var useArap = algorithm == PuppetDeformationAlgorithm.Arap
                && arapEffect is not null
                && pinCount > 0
                && imageWidth > 0
                && imageHeight > 0;

            if (isFirst
                || this.algorithm != algorithm
                || this.pinCount != pinCount
                || this.stiffness != stiffness
                || this.imageWidth != imageWidth
                || this.imageHeight != imageHeight
                || this.apply != apply
                || !PinSamplesMatchBuffer(samples))
            {
                if (useArap)
                {
                    FillPinDataBuffer(samples);
                    UpdateArapEffect(samples, apply, imageWidth, imageHeight);

                    //終端のMLSエフェクトはパススルー(PinCount=0)として使う
                    effect.PinCount = 0;
                    effect.TightLocalLeft = 0;
                    effect.TightLocalTop = 0;
                    effect.TightLocalRight = 0;
                    effect.TightLocalBottom = 0;
                }
                else
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
                }

                cachedControllers = [.. BuildControllers(samples)];
            }

            SetWiring(useArap);

            isFirst = false;
            this.algorithm = algorithm;
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

        void FillPinDataBuffer(List<PinSample> samples)
        {
            var maxPins = PuppetDeformationCustomEffect.MaxPins;
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                pinDataBuffer[i * 4 + 0] = s.Rest.X;
                pinDataBuffer[i * 4 + 1] = s.Rest.Y;
                pinDataBuffer[i * 4 + 2] = s.Current.X;
                pinDataBuffer[i * 4 + 3] = s.Current.Y;
            }
            Array.Clear(pinDataBuffer, samples.Count * 4, (maxPins - samples.Count) * 4);
        }

        void UpdateArapEffect(List<PinSample> samples, bool apply, float width, float height)
        {
            if (arapEffect is null)
                return;

            //メッシュは画像サイズに依存する。透明領域で隔てられた部位を独立して変形できるよう、
            //完全透明な三角形は常にメッシュから除去する。
            //アルファは再構築時点の入力から取得する（動画では以後のアルファ変化に追従しない）
            if (arapMesh is null || arapMesh.Width != width || arapMesh.Height != height)
            {
                var mesh = ArapGridMesh.Create(width, height, PuppetDeformationArapCustomEffect.MaxTriangles, ArapMinSpacing);
                var keep = BuildAlphaTriangleMask(mesh, width, height);
                if (keep is not null)
                    mesh = mesh.WithTriangleMask(keep);
                arapMesh = mesh;
                arapDeformer = null;
                deformedPositions = new Vector2[arapMesh.VertexCount];
            }
            arapVertexData ??= new byte[PuppetDeformationArapCustomEffect.MaxVertices * PuppetDeformationArapCustomEffect.VertexStride];

            //ピンのレスト位置が変わったら行列分解を作り直す
            var restsChanged = arapRests is null || arapRests.Length != samples.Count;
            if (!restsChanged)
            {
                for (var i = 0; i < samples.Count; i++)
                {
                    if (arapRests![i] != samples[i].Rest)
                    {
                        restsChanged = true;
                        break;
                    }
                }
            }
            if (arapDeformer is null || restsChanged)
            {
                arapRests = new Vector2[samples.Count];
                for (var i = 0; i < samples.Count; i++)
                    arapRests[i] = samples[i].Rest;
                arapDeformer = ArapDeformer.TryCreate(arapMesh, arapRests);
            }

            var rests = arapMesh.RestPositions;
            if (arapDeformer is null)
            {
                Array.Copy(rests, deformedPositions!, rests.Length);
            }
            else
            {
                var targets = new Vector2[samples.Count];
                for (var i = 0; i < samples.Count; i++)
                    targets[i] = apply ? samples[i].Current : samples[i].Rest;
                arapDeformer.Solve(targets, ArapIterations, deformedPositions!);
            }

            //三角形リストへ展開して頂点データを書き込み、変形後のAABBを求める
            var deformed = deformedPositions!;
            var triangleIndices = arapMesh.TriangleIndices;
            var vertexFloats = MemoryMarshal.Cast<byte, float>(arapVertexData.AsSpan());
            var o = 0;
            foreach (var v in triangleIndices)
            {
                vertexFloats[o++] = deformed[v].X;
                vertexFloats[o++] = deformed[v].Y;
                vertexFloats[o++] = rests[v].X;
                vertexFloats[o++] = rests[v].Y;
            }

            //AABBは描画に使う頂点（残存三角形の頂点）のみから求める
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var v in triangleIndices)
            {
                var p = deformed[v];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            //ラスタライズの丸め対策に1pxの余白を持たせる
            const float margin = 1f;
            arapEffect.VertexCount = triangleIndices.Length;
            arapEffect.VertexData = arapVertexData;
            arapEffect.TightLocalLeft = minX - margin;
            arapEffect.TightLocalTop = minY - margin;
            arapEffect.TightLocalRight = maxX + margin;
            arapEffect.TightLocalBottom = maxY + margin;
        }

        /// <summary>
        /// 入力画像のアルファをCPUに読み戻し、不透明ピクセルを含む三角形の残存フラグを作る。
        /// 透明領域で隔てられた部位（腕・脚など）のメッシュ接続を切るために使う。
        /// 読み戻せない場合（入力なし・サイズ超過など）はnullを返し、呼び出し側はマスクなしで続行する。
        /// </summary>
        unsafe bool[]? BuildAlphaTriangleMask(ArapGridMesh mesh, float width, float height)
        {
            if (deviceContext is null || input is null)
                return null;

            var w = (int)MathF.Ceiling(width);
            var h = (int)MathF.Ceiling(height);
            if (w <= 0 || h <= 0 || w > ArapAlphaReadbackMaxSize || h > ArapAlphaReadbackMaxSize)
                return null;

            var bounds = deviceContext.GetImageLocalBounds(input);

            var gpuProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                deviceContext.Dpi.Width,
                deviceContext.Dpi.Height,
                BitmapOptions.Target);
            var cpuProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                deviceContext.Dpi.Width,
                deviceContext.Dpi.Height,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw);

            using var gpuBitmap = deviceContext.CreateBitmap(new SizeI(w, h), gpuProps);
            using var cpuBitmap = deviceContext.CreateBitmap(new SizeI(w, h), cpuProps);

            deviceContext.Target = gpuBitmap;
            deviceContext.BeginDraw();
            deviceContext.Clear(new Color4(0f, 0f, 0f, 0f));
            deviceContext.DrawImage(
                input,
                new Vector2(-bounds.Left, -bounds.Top),
                null,
                InterpolationMode.NearestNeighbor,
                CompositeMode.SourceCopy);
            deviceContext.EndDraw();
            deviceContext.Target = null;

            cpuBitmap.CopyFromBitmap(gpuBitmap);

            var keep = new bool[mesh.FullTriangleCount];
            var map = cpuBitmap.Map(MapOptions.Read);
            try
            {
                var ptr = (byte*)map.Bits;
                var halfW = width * 0.5f;
                var halfH = height * 0.5f;
                for (var y = 0; y < h; y++)
                {
                    var row = ptr + (long)y * map.Pitch;
                    var ly = y + 0.5f - halfH;
                    for (var x = 0; x < w; x++)
                    {
                        //B8G8R8A8のアルファは4バイト目。少しでも不透明ならその三角形を残す
                        if (row[x * 4 + 3] == 0)
                            continue;
                        keep[mesh.GetFullTriangleIndexAt(new Vector2(x + 0.5f - halfW, ly))] = true;
                    }
                }
            }
            finally
            {
                cpuBitmap.Unmap();
            }
            return keep;
        }

        void SetWiring(bool useArap)
        {
            if (useArapWiring == useArap)
                return;
            useArapWiring = useArap;
            ApplyWiring();
        }

        void ApplyWiring()
        {
            if (effect is null)
                return;

            if (useArapWiring && arapEffect is not null)
            {
                arapEffect.SetInput(0, input, true);
                using var arapOutput = arapEffect.Output;
                effect.SetInput(0, arapOutput, true);
            }
            else
            {
                arapEffect?.SetInput(0, null, true);
                effect.SetInput(0, input, true);
            }
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
            //基準ピンの編集はアイテム編集UIのピン配置キャンバスで行うため、
            //プレビュー上は移動ピンの操作に絞って表示を簡素化する
            var controllers = new List<VideoEffectController>(samples.Count * 2);

            foreach (var s in samples)
            {
                var pin = item.Pins[s.PinIndex];

                if (!s.IsEnabled)
                {
                    //無効ピン(アンカー)は操作できないマーカーのみ表示する
                    controllers.Add(new VideoEffectController(item, [
                        new ControllerPoint(new Vector3(s.Rest.X, s.Rest.Y, 0f)) { Shape = VideoControllerPointShape.SmallCircle }
                    ]));
                    continue;
                }

                //選択中のピンのみ基準位置との接続線を表示する
                if (pin.IsOffsetSelected)
                {
                    controllers.Add(new VideoEffectController(item, [
                        new ControllerPoint(new Vector3(s.Rest.X, s.Rest.Y, 0f)),
                        new ControllerPoint(new Vector3(s.Current.X, s.Current.Y, 0f)),
                    ])
                    { Connection = VideoControllerPointConnection.Line });
                }

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
                    Shape = VideoControllerPointShape.Circle
                };
                controllers.Add(new VideoEffectController(item, [offsetPoint]));
            }

            return controllers;
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

            //ARAP用エフェクト。非対応環境(頂点シェーダーを読み込めない等)ではMLSにフォールバックする
            arapEffect = new PuppetDeformationArapCustomEffect(devices);
            if (!arapEffect.IsEnabled)
            {
                arapEffect.Dispose();
                arapEffect = null;
            }
            else
            {
                disposer.Collect(arapEffect);
            }

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            ApplyWiring();
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            arapEffect?.SetInput(0, null, true);
            gpuCache = null;
            cachedControllers = ImmutableList<VideoEffectController>.Empty;
            isFirst = true;
            useArapWiring = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gpuCache = null;
                deviceContext = null;
                effect = null;
                arapEffect = null;
                arapMesh = null;
                arapDeformer = null;
                arapRests = null;
                deformedPositions = null;
                arapVertexData = null;
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
