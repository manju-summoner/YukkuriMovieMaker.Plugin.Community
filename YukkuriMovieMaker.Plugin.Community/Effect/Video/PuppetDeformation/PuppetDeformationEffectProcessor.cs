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
#if DEBUG
using D2DEffects = Vortice.Direct2D1.Effects;
#endif

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationEffectProcessor(IGraphicsDevicesAndContext devices, PuppetDeformationEffect item) : VideoEffectProcessorBase(devices)
    {
        //反復回数は固定（品質は支持拘束が担保する。反復を増やしてもドラッグ操作が重くなるだけで品質は変わらない）
        const int ArapIterations = 6;
        const float ArapMinSpacing = 8f;
        //ボーンジョイント由来の拘束点を示すPinSample.PinIndex（item.Pinsに対応しない）
        const int JointPinIndex = -1;
        //アルファ読み戻しを行う入力サイズの上限（これを超える場合は切り離しをスキップ）
        const int ArapAlphaReadbackMaxSize = 4096;

        readonly PuppetDeformationEffect item = item;
        readonly float[] pinDataBuffer = new float[PuppetDeformationCustomEffect.MaxPins * 4];
        readonly bool[] offsetSelectionCache = new bool[PuppetDeformationCustomEffect.MaxPins];
        //1ボーンあたり (joint.x, joint.y, angle, parentIndex, scale) の5要素
        readonly float[] boneDataBuffer = new float[PuppetDeformationEffect.BoneCapacity * 5];
        //ピンのボーン割当（ボーンindex、未割当は-1）。ハンドル表示の切替検知に使う
        readonly int[] pinBoneIndexCache = new int[PuppetDeformationCustomEffect.MaxPins];

        PuppetDeformationCustomEffect? effect;
        PuppetDeformationArapCustomEffect? arapEffect;
        ID2D1DeviceContext? deviceContext;
        PinGpuCache? gpuCache;
        ImmutableList<VideoEffectController> cachedControllers = ImmutableList<VideoEffectController>.Empty;

        //ARAP用キャッシュ
        IArapMesh? arapMesh;
        ArapDeformer? arapDeformer;
        Vector2[]? arapRests;
        Vector2[]? deformedPositions;
        byte[]? arapVertexData;
        bool useArapWiring;

#if DEBUG
        //デバッグ用メッシュ表示: 最終出力にワイヤーフレームのコマンドリストを合成する
        D2DEffects.Composite? debugMeshComposite;
        ID2D1CommandList? debugMeshCommandList;
        bool debugMeshVisible;
        Vector2 debugMeshCenter;
#endif

        bool isFirst = true;
        bool apply = true;
        PuppetDeformationAlgorithm algorithm = PuppetDeformationAlgorithm.Mls;
        int constraintCount;
        float stiffness;
        float imageWidth;
        float imageHeight;
        int boneCount;

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
                var scale = 1f;
                if (bone.IsEnabled)
                {
                    var angleDeg = bone.Angle.GetValue(frame, length, fps);
                    angleRad = (float)(angleDeg * Math.PI / 180) + swayAngles[i];
                    scale = (float)(bone.Scale.GetValue(frame, length, fps) / 100);
                }
                boneSamples.Add(new PuppetBoneEvaluator.BoneSample(bone.Id, bone.ParentId, new Vector2(jx, jy), angleRad, scale));
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
            //回転していないジョイントはアンカーとして働き、回転はFKでジョイントを動かして画像を引っ張る。
            //ルート以外のジョイントは自身の回転（親ジョイント中心のセグメント回転）でも動き、ルートのジョイントは自身の回転では動かない
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
            var inputCenter = new Vector2(
                (inputBounds.Left + inputBounds.Right) * 0.5f,
                (inputBounds.Top + inputBounds.Bottom) * 0.5f);
            //HLSL・ARAPメッシュ・変形後Boundsは入力画像の中央を原点とする。
            //プレビュー用コントローラーのアイテム座標は保ち、GPUに渡す拘束点だけを中央原点へ変換する。
            var gpuSamples = ConvertToCenteredSamples(samples, inputCenter);

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
                || !PinSamplesMatchBuffer(gpuSamples);
            //ピンが割り当てられていないボーンの変化はGPU側に影響しないため、コントローラー再構築のみ行う
            var bonesDirty = isFirst
                || this.boneCount != boneCount
                || !BoneSamplesMatchBuffer(boneSamples, boneIndexById);

            if (gpuDirty)
            {
                if (useArap)
                {
                    FillPinDataBuffer(gpuSamples);
                    UpdateArapEffect(gpuSamples, apply, imageWidth, imageHeight);

                    //終端のMLSエフェクトはパススルー(PinCount=0)として使う
                    effect.PinCount = 0;
                    effect.TightLocalLeft = 0;
                    effect.TightLocalTop = 0;
                    effect.TightLocalRight = 0;
                    effect.TightLocalBottom = 0;
                }
                else
                {
                    gpuCache = BuildGpuCache(stiffness, imageWidth, imageHeight, gpuSamples);

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
                cachedControllers = [.. BuildControllers(pins, samples, bones, boneSamples, boneWorlds)];
            }

            SetWiring(useArap);

#if DEBUG
            UpdateDebugMeshOverlay(useArap && item.ShowDebugMesh, gpuDirty, inputCenter);
#endif

            isFirst = false;
            this.algorithm = algorithm;
            this.constraintCount = constraintCount;
            this.stiffness = stiffness;
            this.imageWidth = imageWidth;
            this.imageHeight = imageHeight;
            this.apply = apply;
            this.boneCount = boneCount;

            return effectDescription.DrawDescription with
            {
                Controllers = cachedControllers
            };
        }

        static List<PinSample> ConvertToCenteredSamples(List<PinSample> samples, Vector2 inputCenter)
        {
            var result = new List<PinSample>(samples.Count);
            foreach (var sample in samples)
            {
                result.Add(new PinSample(
                    sample.PinIndex,
                    sample.Rest - inputCenter,
                    sample.Current - inputCenter,
                    sample.IsEnabled));
            }
            return result;
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
                if (boneDataBuffer[i * 5 + 0] != s.Joint.X) return false;
                if (boneDataBuffer[i * 5 + 1] != s.Joint.Y) return false;
                if (boneDataBuffer[i * 5 + 2] != s.AngleRadians) return false;
                if (boneDataBuffer[i * 5 + 3] != parentIndex) return false;
                if (boneDataBuffer[i * 5 + 4] != s.Scale) return false;
            }
            return true;
        }

        void FillBoneDataBuffer(List<PuppetBoneEvaluator.BoneSample> samples, Dictionary<Guid, int> indexById)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var parentIndex = indexById.TryGetValue(s.ParentId, out var pi) ? pi : -1;
                boneDataBuffer[i * 5 + 0] = s.Joint.X;
                boneDataBuffer[i * 5 + 1] = s.Joint.Y;
                boneDataBuffer[i * 5 + 2] = s.AngleRadians;
                boneDataBuffer[i * 5 + 3] = parentIndex;
                boneDataBuffer[i * 5 + 4] = s.Scale;
            }
            Array.Clear(boneDataBuffer, samples.Count * 5, (PuppetDeformationEffect.BoneCapacity - samples.Count) * 5);
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
            //アルファ輪郭に沿ったメッシュを構築する（構築できない場合はグリッド＋三角形間引きへフォールバック）。
            //アルファは再構築時点の入力から取得する（動画では以後のアルファ変化に追従しない）
            if (arapMesh is null || arapMesh.Width != width || arapMesh.Height != height)
            {
                arapMesh = BuildArapMesh(width, height);
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
        /// 画像サイズに応じたARAPメッシュを構築する。
        /// アルファ輪郭に沿ったメッシュを優先し、構築できない場合（読み戻し不可・
        /// 病的な輪郭・三角形予算超過など）は従来のグリッド＋完全透明三角形の間引きへフォールバックする。
        /// </summary>
        IArapMesh BuildArapMesh(float width, float height)
        {
            var alpha = TryReadbackAlphaMask(width, height);
            if (alpha is var (opaque, w, h))
            {
                var contourMesh = ArapContourMeshBuilder.TryBuild(
                    opaque, w, h, width, height,
                    PuppetDeformationArapCustomEffect.MaxTriangles, ArapMinSpacing);
                if (contourMesh is not null)
                    return contourMesh;
            }

            var grid = ArapGridMesh.Create(width, height, PuppetDeformationArapCustomEffect.MaxTriangles, ArapMinSpacing);
            if (alpha is var (opaque2, w2, h2))
            {
                var keep = BuildAlphaTriangleMask(grid, opaque2, w2, h2, width, height);
                grid = grid.WithTriangleMask(keep);
            }
            return grid;
        }

        /// <summary>
        /// グリッドメッシュ用に、不透明ピクセルを含む三角形の残存フラグを作る。
        /// 透明領域で隔てられた部位（腕・脚など）のメッシュ接続を切るために使う。
        /// </summary>
        static bool[] BuildAlphaTriangleMask(ArapGridMesh mesh, bool[] opaque, int w, int h, float width, float height)
        {
            var keep = new bool[mesh.FullTriangleCount];
            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            for (var y = 0; y < h; y++)
            {
                var ly = y + 0.5f - halfH;
                for (var x = 0; x < w; x++)
                {
                    if (!opaque[y * w + x])
                        continue;
                    keep[mesh.GetFullTriangleIndexAt(new Vector2(x + 0.5f - halfW, ly))] = true;
                }
            }
            return keep;
        }

        /// <summary>
        /// 入力画像のアルファをCPUに読み戻し、不透明ピクセルのマスクを作る。
        /// 読み戻せない場合（入力なし・サイズ超過など）はnullを返す。
        /// </summary>
        unsafe (bool[] Opaque, int W, int H)? TryReadbackAlphaMask(float width, float height)
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

            var opaque = new bool[w * h];
            var map = cpuBitmap.Map(MapOptions.Read);
            try
            {
                var ptr = (byte*)map.Bits;
                for (var y = 0; y < h; y++)
                {
                    var row = ptr + (long)y * map.Pitch;
                    for (var x = 0; x < w; x++)
                    {
                        //B8G8R8A8のアルファは4バイト目。少しでも不透明なら前景として扱う
                        opaque[y * w + x] = row[x * 4 + 3] != 0;
                    }
                }
            }
            finally
            {
                cpuBitmap.Unmap();
            }
            return (opaque, w, h);
        }

#if DEBUG
        /// <summary>
        /// メッシュ分割状況のワイヤーフレームを出力に合成する（デバッグ用）。
        /// 変形後の頂点位置で描くため、ピンを動かした際のメッシュの追従も確認できる。
        /// </summary>
        void UpdateDebugMeshOverlay(bool show, bool meshUpdated, Vector2 inputCenter)
        {
            if (debugMeshComposite is null || deviceContext is null)
                return;

            if (!show || arapMesh is null || deformedPositions is null)
            {
                HideDebugMeshOverlay();
                return;
            }

            if (debugMeshVisible && !meshUpdated && debugMeshCenter == inputCenter)
                return;

            //ワイヤーフレームはメッシュと同じ中央原点座標で描き、入力画像のシーン座標へオフセットする
            var commandList = deviceContext.CreateCommandList();
            deviceContext.Target = commandList;
            deviceContext.BeginDraw();
            deviceContext.Clear(null);
            using (var brush = deviceContext.CreateSolidColorBrush(new Color4(0.2f, 1f, 0.5f, 0.85f)))
            {
                //三角形リストから重複なしのエッジを描く
                var indices = arapMesh.TriangleIndices;
                var deformed = deformedPositions;
                var drawn = new HashSet<long>();
                void DrawEdge(int a, int b)
                {
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (!drawn.Add(key))
                        return;
                    deviceContext.DrawLine(deformed[a] + inputCenter, deformed[b] + inputCenter, brush, 1f);
                }
                for (var t = 0; t < indices.Length; t += 3)
                {
                    DrawEdge(indices[t], indices[t + 1]);
                    DrawEdge(indices[t + 1], indices[t + 2]);
                    DrawEdge(indices[t + 2], indices[t]);
                }
            }
            deviceContext.EndDraw();
            deviceContext.Target = null;
            commandList.Close();

            debugMeshComposite.InputCount = 2;
            debugMeshComposite.SetInput(1, commandList, true);
            debugMeshCommandList?.Dispose();
            debugMeshCommandList = commandList;
            debugMeshVisible = true;
            debugMeshCenter = inputCenter;
        }

        void HideDebugMeshOverlay()
        {
            if (!debugMeshVisible)
                return;
            debugMeshComposite?.SetInput(1, null, true);
            if (debugMeshComposite is not null)
                debugMeshComposite.InputCount = 1;
            debugMeshCommandList?.Dispose();
            debugMeshCommandList = null;
            debugMeshVisible = false;
        }
#endif

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
            Matrix3x2[] boneWorlds)
        {
            //基準ピンの編集はアイテム編集UIのピン配置キャンバスで行うため、
            //プレビュー上は移動ピンの操作に絞って表示を簡素化する
            var controllers = new List<VideoEffectController>(samples.Count * 2 + boneSamples.Count * 2);

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
        /// ルート以外はジョイント自体がハンドルとなり、ドラッグすると親ジョイント（セグメントの根元）を中心に回る。
        /// 分岐点から出る各セグメントは角度を子側が持つため、それぞれ独立して回転できる。
        /// ルートはジョイントから伸びる固定レバーの先端をドラッグして全体を回す。
        /// </summary>
        void AddBoneControllers(
            List<VideoEffectController> controllers,
            ImmutableList<PuppetDeformation> pins,
            ImmutableList<PuppetBone> bones,
            List<PuppetBoneEvaluator.BoneSample> boneSamples,
            Matrix3x2[] boneWorlds)
        {
            //ルートボーン（およびジョイントが親と重なるボーン）のレバー長(px)
            const float LeverLength = 80f;

            for (var i = 0; i < boneSamples.Count; i++)
            {
                var bone = bones[i];
                var s = boneSamples[i];
                var world = boneWorlds[i];
                var jointWorld = Vector2.Transform(s.Joint, world);

                //親が解決できるボーンは「親ジョイント→自分」のセグメントとして扱う（評価側の親解決と同じ条件）
                var parentIndex = -1;
                if (s.ParentId != Guid.Empty && s.ParentId != s.Id)
                {
                    for (var c = 0; c < boneSamples.Count; c++)
                    {
                        if (c != i && boneSamples[c].Id == s.ParentId)
                        {
                            parentIndex = c;
                            break;
                        }
                    }
                }

                var hasChild = false;
                for (var c = 0; c < boneSamples.Count; c++)
                {
                    if (c != i && boneSamples[c].ParentId == s.Id)
                    {
                        hasChild = true;
                        break;
                    }
                }
                var hasPin = pins.Any(p => p.IsEnabled && p.BoneId == s.Id);

                Vector2 pivotRest;
                Vector2 tipRest;
                bool canRotate;
                if (parentIndex >= 0)
                {
                    //セグメント回転：回転中心は親ジョイント、ハンドルは自分のジョイント。
                    //回すと自分のジョイント（拘束点）が動いて画像を引っ張るため、子やピンが無くても回す意味がある
                    pivotRest = boneSamples[parentIndex].Joint;
                    var jointMoves = Vector2.DistanceSquared(s.Joint, pivotRest) > 1f;
                    //ジョイントが親と重なっている場合は+X方向の固定レバーで代用する
                    tipRest = jointMoves ? s.Joint : pivotRest + new Vector2(LeverLength, 0f);
                    canRotate = bone.IsEnabled && (jointMoves || hasChild || hasPin);
                }
                else
                {
                    //ルートは自身のジョイントを中心に全体を回す。子ジョイントは各セグメントのハンドルと重なるため固定レバーにする
                    pivotRest = s.Joint;
                    tipRest = s.Joint + new Vector2(LeverLength, 0f);
                    canRotate = bone.IsEnabled && (hasChild || hasPin);
                }

                //回しても何も動かないボーン（無効、または回す対象が無い）はハンドルを出さずマーカーのみ表示する
                if (!canRotate)
                {
                    controllers.Add(new VideoEffectController(item, [
                        new ControllerPoint(new Vector3(jointWorld.X, jointWorld.Y, 0f)) { Shape = VideoControllerPointShape.SmallCircle }
                    ]));
                    continue;
                }

                //pivotは自身のローカル回転の不動点なので、自身のworldで変換しても親ワールドでの位置と一致する
                var pivotWorld = Vector2.Transform(pivotRest, world);
                var tipWorld = Vector2.Transform(tipRest, world);

                controllers.Add(new VideoEffectController(item, [
                    new ControllerPoint(new Vector3(pivotWorld.X, pivotWorld.Y, 0f)) { Shape = VideoControllerPointShape.SmallCircle },
                    new ControllerPoint(new Vector3(tipWorld.X, tipWorld.Y, 0f)),
                ])
                { Connection = VideoControllerPointConnection.Line });

                var handlePoint = new ControllerPoint(
                    new Vector3(tipWorld.X, tipWorld.Y, 0f),
                    arg =>
                    {
                        //先端の移動を回転中心（親ジョイント／ルートは自身のジョイント）周りの回転角に変換する。
                        //角度変更→次フレームでコントローラーが再構築されるため、各イベントの差分だけ加算すればよい
                        var baseAngle = Math.Atan2(tipWorld.Y - pivotWorld.Y, tipWorld.X - pivotWorld.X);
                        var movedAngle = Math.Atan2(tipWorld.Y + arg.Delta.Y - pivotWorld.Y, tipWorld.X + arg.Delta.X - pivotWorld.X);
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

#if DEBUG
            //デバッグ用メッシュ表示: 最終出力の上にワイヤーフレームを合成できるようCompositeを挟む。
            //非表示時はInputCount=1のパススルーとして働く
            debugMeshComposite = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 1 };
            disposer.Collect(debugMeshComposite);
            using (var effectOutput = effect.Output)
                debugMeshComposite.SetInput(0, effectOutput, true);
            var output = debugMeshComposite.Output;
            disposer.Collect(output);
            return output;
#else
            var output = effect.Output;
            disposer.Collect(output);
            return output;
#endif
        }

        protected override void setInput(ID2D1Image? input)
        {
            ApplyWiring();
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            arapEffect?.SetInput(0, null, true);
#if DEBUG
            //Compositeのinput0はeffect.Output（内部接続）なので外さない。コマンドリストのみ解放する
            HideDebugMeshOverlay();
#endif
            gpuCache = null;
            cachedControllers = ImmutableList<VideoEffectController>.Empty;
            isFirst = true;
            useArapWiring = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
#if DEBUG
                debugMeshCommandList?.Dispose();
                debugMeshCommandList = null;
                debugMeshComposite = null;
#endif
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
