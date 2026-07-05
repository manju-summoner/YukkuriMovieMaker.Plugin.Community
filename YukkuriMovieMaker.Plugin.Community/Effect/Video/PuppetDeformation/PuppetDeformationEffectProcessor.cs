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
        //ボーンジョイント由来の拘束点を示すPinSample.PinIndex（item.Pinsに対応しない）
        const int JointPinIndex = -1;
        //アルファ読み戻しを行う入力サイズの上限（これを超える場合は切り離しをスキップ）
        const int ArapAlphaReadbackMaxSize = 4096;

        readonly PuppetDeformationEffect item = item;
        readonly float[] pinDataBuffer = new float[PuppetDeformationCustomEffect.MaxPins * 4];
        readonly bool[] offsetSelectionCache = new bool[PuppetDeformationCustomEffect.MaxPins];
        readonly float[] boneDataBuffer = new float[PuppetDeformationEffect.BoneCapacity * 4];
        //ピンのボーン割当（ボーンindex、未割当は-1）。ハンドル表示の切替検知に使う
        readonly int[] pinBoneIndexCache = new int[PuppetDeformationCustomEffect.MaxPins];

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
        int constraintCount;
        float stiffness;
        float imageWidth;
        float imageHeight;
        int boneCount;
        bool showBones;

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
            var showBones = item.ShowBones;

            //ボーンを評価する。回転(角度+揺れ)を親から子へ合成したワールド変換を作り、割当ピンの移動先計算に使う
            var bones = item.Bones;
            var boneCount = Math.Min(bones.Count, PuppetDeformationEffect.BoneCapacity);
            var timeSec = fps > 0 ? (double)frame / fps : 0.0;
            //1パス目: 揺れの発生源を集め、伝播込みの揺れ角度を求める（無効ボーンは揺れを発生させない）
            var swaySamples = new List<PuppetBoneEvaluator.SwaySample>(boneCount);
            for (var i = 0; i < boneCount; i++)
            {
                var bone = bones[i];
                var swayAmp = bone.IsEnabled ? bone.SwayAngle.GetValue(frame, length, fps) : 0.0;
                swaySamples.Add(new PuppetBoneEvaluator.SwaySample(bone.Id, bone.ParentId, swayAmp, bone.SwayPeriod, bone.SwayPhase, bone.SwayFlexibility, bone.SwayPropagation));
            }
            var swayAngles = PuppetBoneEvaluator.ComputeSwayAngles(swaySamples, timeSec);

            //2パス目: 角度+揺れからFK用のサンプルを作る
            var boneSamples = new List<PuppetBoneEvaluator.BoneSample>(boneCount);
            var boneIndexById = new Dictionary<Guid, int>(boneCount);
            for (var i = 0; i < boneCount; i++)
            {
                var bone = bones[i];
                var jx = (float)bone.JointX.GetValue(frame, length, fps);
                var jy = (float)bone.JointY.GetValue(frame, length, fps);
                var angleRad = 0f;
                if (bone.IsEnabled)
                {
                    var angleDeg = bone.Angle.GetValue(frame, length, fps);
                    angleRad = (float)(angleDeg * Math.PI / 180) + swayAngles[i];
                }
                boneSamples.Add(new PuppetBoneEvaluator.BoneSample(bone.Id, bone.ParentId, new Vector2(jx, jy), angleRad));
                boneIndexById.TryAdd(bone.Id, i);
            }
            var boneWorlds = PuppetBoneEvaluator.ComputeWorldTransforms(boneSamples);

            var pinCount = Math.Min(pins.Count, PuppetDeformationCustomEffect.MaxPins);
            var samples = new List<PinSample>(pinCount);
            var assignmentChanged = false;
            for (var i = 0; i < pinCount; i++)
            {
                var pin = pins[i];
                var rx = (float)pin.RestX.GetValue(frame, length, fps);
                var ry = (float)pin.RestY.GetValue(frame, length, fps);
                var ox = pin.IsEnabled ? (float)pin.OffsetX.GetValue(frame, length, fps) : 0f;
                var oy = pin.IsEnabled ? (float)pin.OffsetY.GetValue(frame, length, fps) : 0f;
                var rest = new Vector2(rx, ry);
                var current = new Vector2(rx + ox, ry + oy);
                //ボーン割当ピンはレスト位置をボーンのワールド変換で回し、その上に手動オフセットを乗せる。
                //レスト位置自体は動かさない（ARAPの行列分解キャッシュを保つため）
                var assignedBoneIndex = -1;
                if (pin.IsEnabled && pin.BoneId != Guid.Empty && boneIndexById.TryGetValue(pin.BoneId, out var boneIndex))
                {
                    assignedBoneIndex = boneIndex;
                    current = Vector2.Transform(rest, boneWorlds[boneIndex]) + new Vector2(ox, oy);
                }
                //割当の変化はボーンが無回転だとピン位置に現れないため、ハンドル表示の更新用に別途検知する
                if (pinBoneIndexCache[i] != assignedBoneIndex)
                {
                    pinBoneIndexCache[i] = assignedBoneIndex;
                    assignmentChanged = true;
                }
                samples.Add(new PinSample(i, rest, current, pin.IsEnabled));
            }

            //ボーンのジョイントを拘束点として追加する。
            //回転していないジョイントはアンカーとして働き、回転はFKで子孫ジョイントを動かして画像を引っ張る。
            //自身の回転はジョイント自体を動かさないため、currentには親チェーンの変換だけが乗る
            for (var i = 0; i < boneCount; i++)
            {
                if (samples.Count >= PuppetDeformationCustomEffect.MaxPins)
                    break;
                var s = boneSamples[i];
                var current = Vector2.Transform(s.Joint, boneWorlds[i]);
                samples.Add(new PinSample(JointPinIndex, s.Joint, current, true));
            }
            var constraintCount = samples.Count;

            var inputBounds = deviceContext is not null && input is not null
                ? deviceContext.GetImageLocalBounds(input)
                : default;
            var imageWidth = inputBounds.Right - inputBounds.Left;
            var imageHeight = inputBounds.Bottom - inputBounds.Top;

            var useArap = algorithm == PuppetDeformationAlgorithm.Arap
                && arapEffect is not null
                && constraintCount > 0
                && imageWidth > 0
                && imageHeight > 0;

            //選択状態の変化はコントローラー表示にのみ影響するため、GPU側の更新とは別に検知する
            var selectionChanged = UpdateOffsetSelectionCache(pins, pinCount);

            var gpuDirty = isFirst
                || this.algorithm != algorithm
                || this.constraintCount != constraintCount
                || this.stiffness != stiffness
                || this.imageWidth != imageWidth
                || this.imageHeight != imageHeight
                || this.apply != apply
                || !PinSamplesMatchBuffer(samples);
            //ピンが割り当てられていないボーンの変化はGPU側に影響しないため、コントローラー再構築のみ行う
            var bonesDirty = isFirst
                || this.showBones != showBones
                || this.boneCount != boneCount
                || !BoneSamplesMatchBuffer(boneSamples, boneIndexById);

            if (gpuDirty)
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
                    effect.PinCount = apply ? constraintCount : 0;
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
            }

            if (gpuDirty || bonesDirty || selectionChanged || assignmentChanged)
            {
                FillBoneDataBuffer(boneSamples, boneIndexById);
                cachedControllers = [.. BuildControllers(pins, samples, bones, boneSamples, boneWorlds, showBones)];
            }

            SetWiring(useArap);

            isFirst = false;
            this.algorithm = algorithm;
            this.constraintCount = constraintCount;
            this.stiffness = stiffness;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
            this.apply = apply;
            this.boneCount = boneCount;
            this.showBones = showBones;

            return effectDescription.DrawDescription with
            {
                Controllers = cachedControllers
            };
        }

        bool UpdateOffsetSelectionCache(ImmutableList<PuppetDeformation> pins, int pinCount)
        {
            var changed = false;
            for (var i = 0; i < pinCount; i++)
            {
                var selected = pins[i].IsOffsetSelected;
                if (offsetSelectionCache[i] != selected)
                {
                    offsetSelectionCache[i] = selected;
                    changed = true;
                }
            }
            return changed;
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

        bool BoneSamplesMatchBuffer(List<PuppetBoneEvaluator.BoneSample> samples, Dictionary<Guid, int> indexById)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var parentIndex = indexById.TryGetValue(s.ParentId, out var pi) ? pi : -1;
                if (boneDataBuffer[i * 4 + 0] != s.Joint.X) return false;
                if (boneDataBuffer[i * 4 + 1] != s.Joint.Y) return false;
                if (boneDataBuffer[i * 4 + 2] != s.AngleRadians) return false;
                if (boneDataBuffer[i * 4 + 3] != parentIndex) return false;
            }
            return true;
        }

        void FillBoneDataBuffer(List<PuppetBoneEvaluator.BoneSample> samples, Dictionary<Guid, int> indexById)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var parentIndex = indexById.TryGetValue(s.ParentId, out var pi) ? pi : -1;
                boneDataBuffer[i * 4 + 0] = s.Joint.X;
                boneDataBuffer[i * 4 + 1] = s.Joint.Y;
                boneDataBuffer[i * 4 + 2] = s.AngleRadians;
                boneDataBuffer[i * 4 + 3] = parentIndex;
            }
            Array.Clear(boneDataBuffer, samples.Count * 4, (PuppetDeformationEffect.BoneCapacity - samples.Count) * 4);
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

        //ピン・ボーンのリストは編集操作で別スレッドから差し替わる可能性があるため、
        //Update冒頭で取得したスナップショットを受け取り、item.Pins/item.Bonesを再取得しない
        List<VideoEffectController> BuildControllers(
            ImmutableList<PuppetDeformation> pins,
            List<PinSample> samples,
            ImmutableList<PuppetBone> bones,
            List<PuppetBoneEvaluator.BoneSample> boneSamples,
            Matrix3x2[] boneWorlds,
            bool showBones)
        {
            //基準ピンの編集はアイテム編集UIのピン配置キャンバスで行うため、
            //プレビュー上は移動ピンの操作に絞って表示を簡素化する
            var controllers = new List<VideoEffectController>(samples.Count * 2 + boneSamples.Count * 2);

            if (showBones)
                AddBoneControllers(controllers, pins, bones, boneSamples, boneWorlds);

            //選択ハイライトは複数選択の把握が目的のため、1本だけの選択では表示しない
            var selectedOffsetCount = pins.Count(p => p.IsOffsetSelected);
            var showSelectionHighlight = selectedOffsetCount > 1;

            foreach (var s in samples)
            {
                //ジョイント由来の拘束点はボーン側のコントローラーで操作するため、ピン用の表示は出さない
                if (s.PinIndex == JointPinIndex)
                    continue;

                var pin = pins[s.PinIndex];

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
                    IsSelected = pin.IsOffsetSelected && showSelectionHighlight,
                    Shape = VideoControllerPointShape.Circle
                };
                controllers.Add(new VideoEffectController(item, [offsetPoint]));
            }

            return controllers;
        }

        /// <summary>
        /// ボーンの回転ハンドルをプレビューに追加する。
        /// ジョイントから伸びるレバーの先端をドラッグすると角度が変わる。
        /// </summary>
        void AddBoneControllers(
            List<VideoEffectController> controllers,
            ImmutableList<PuppetDeformation> pins,
            ImmutableList<PuppetBone> bones,
            List<PuppetBoneEvaluator.BoneSample> boneSamples,
            Matrix3x2[] boneWorlds)
        {
            //子ボーンを持たないボーンのレバー長(px)
            const float LeverLength = 80f;

            for (var i = 0; i < boneSamples.Count; i++)
            {
                var bone = bones[i];
                var s = boneSamples[i];
                var world = boneWorlds[i];
                var jointWorld = Vector2.Transform(s.Joint, world);

                //レバー先端は子ボーンのジョイント。子がいない(またはジョイントが重なる)場合は+X方向の固定長レバー
                var tipRest = s.Joint + new Vector2(LeverLength, 0f);
                var hasChild = false;
                for (var c = 0; c < boneSamples.Count; c++)
                {
                    if (c == i || boneSamples[c].ParentId != s.Id)
                        continue;
                    hasChild = true;
                    if (Vector2.DistanceSquared(boneSamples[c].Joint, s.Joint) > 1f)
                    {
                        tipRest = boneSamples[c].Joint;
                        break;
                    }
                }

                //回しても何も動かないボーン（無効、または子ボーンも割当ピンもない末端）はハンドルを出さずマーカーのみ表示する
                var canRotate = bone.IsEnabled
                    && (hasChild || pins.Any(p => p.IsEnabled && p.BoneId == s.Id));
                if (!canRotate)
                {
                    controllers.Add(new VideoEffectController(item, [
                        new ControllerPoint(new Vector3(jointWorld.X, jointWorld.Y, 0f)) { Shape = VideoControllerPointShape.SmallCircle }
                    ]));
                    continue;
                }

                var tipWorld = Vector2.Transform(tipRest, world);

                controllers.Add(new VideoEffectController(item, [
                    new ControllerPoint(new Vector3(jointWorld.X, jointWorld.Y, 0f)) { Shape = VideoControllerPointShape.SmallCircle },
                    new ControllerPoint(new Vector3(tipWorld.X, tipWorld.Y, 0f)),
                ])
                { Connection = VideoControllerPointConnection.Line });

                var handlePoint = new ControllerPoint(
                    new Vector3(tipWorld.X, tipWorld.Y, 0f),
                    arg =>
                    {
                        //先端の移動をジョイント周りの回転角に変換する。
                        //角度変更→次フレームでコントローラーが再構築されるため、各イベントの差分だけ加算すればよい
                        var baseAngle = Math.Atan2(tipWorld.Y - jointWorld.Y, tipWorld.X - jointWorld.X);
                        var movedAngle = Math.Atan2(tipWorld.Y + arg.Delta.Y - jointWorld.Y, tipWorld.X + arg.Delta.X - jointWorld.X);
                        var deltaDeg = (movedAngle - baseAngle) * 180.0 / Math.PI;
                        deltaDeg = ((deltaDeg + 540.0) % 360.0) - 180.0;
                        bone.Angle.AddToEachValues(deltaDeg);
                    })
                {
                    Shape = VideoControllerPointShape.Circle
                };
                controllers.Add(new VideoEffectController(item, [handlePoint]));
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
