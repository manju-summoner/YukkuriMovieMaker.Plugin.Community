using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    /// <summary>
    /// パーティクル出力エフェクトの頂点データを毎フレーム構築する。
    ///
    /// 全パラメータのアニメーション対応とスクラブ安全（どのフレームから評価しても同じ絵）を両立するため、
    /// 粒子の結果は時刻から決定論的に導出する。移流キャッシュは計算の省略にのみ使い、
    /// キャッシュmiss時も同じ固定ステップ列を再計算して同じ結果にする：
    /// - 発生タイミング：発生頻度をフレーム毎に積分した累積発生数テーブルから、通し番号kの粒子の発生時刻を逆引きする
    /// - 発生時に確定する属性（初速・サイズ・寿命など）：発生時刻のアニメーション値＋通し番号kのハッシュ乱数から算出
    /// - 風・重力：フレーム毎に積分した累積ドリフトテーブルにより、飛行中の粒子にも時間変化が正確に反映される
    ///
    /// 風・重力の変位は、線形抵抗 v' = -k(v - v_terminal) の解に従い、
    /// 各フレームの終端速度を減衰カーネル (1 - e^(-k(t-s))) で重み付けして粒子ごとに積分する
    /// （風・重力が一定なら従来の「終端速度へ漸近」の閉形式と厳密に一致する）。
    ///
    /// テーブルと属性キャッシュはアニメーションのフィンガープリントで無効化する（編集されたら作り直し）。
    /// 頂点データはピン留めバッファに書き込み、ポインタ渡しでGPUへ直接コピーされる（CrashMeshBuilderと同じ方式）。
    /// 発生時刻・ドリフト変位は「現在時刻からの相対値」で頂点へ焼き込むため、
    /// タイムライン上の絶対時刻が大きくてもfloat精度が落ちない。
    /// </summary>
    internal sealed class ParticleOutputParticleBuilder
    {
        public const int VertexStride = ParticleOutputCustomEffect.VertexStride;
        const int FloatsPerVertex = VertexStride / sizeof(float);

        /// <summary>寿命の上限(s)。生存判定の遡り幅を制限するためにも使う（モデル側のRangeと一致させること）</summary>
        public const double MaxLifetimeSeconds = 20;

        public readonly record struct Result(
            nint Pointer,
            int ByteCount,
            int VertexCount,
            bool HasParticles,
            float Left,
            float Top,
            float Right,
            float Bottom,
            float PerspectiveFocalLength,
            float PerspectiveNearDenominator);

        /// <summary>粒子1個の発生時に確定する属性</summary>
        readonly record struct Attr(
            double Birth,
            double Lifetime,
            float OriginOffsetX,
            float OriginOffsetY,
            float OriginZ,
            float VelocityX,
            float VelocityY,
            float VelocityZ,
            float Swirl,
            float RotationSpeed,
            float SizeScale,
            float EndScale,
            float Fade);

        /// <summary>放出クロックの整数フレーム境界における移流状態</summary>
        readonly record struct AdvectionState(
            long BoundaryFrame,
            Vector3 CurlOffset,
            double DriftX,
            double DriftY,
            double DriftVelocityX,
            double DriftVelocityY);

        /// <summary>粒子1個の描画時刻における移流結果</summary>
        readonly record struct AdvectionResult(
            Vector3 CurlOffset,
            double DriftX,
            double DriftY,
            bool HasBoundaryState,
            AdvectionState BoundaryState);

        struct AdvectionIntegrationState
        {
            public Vector3 CurlOffset;
            public double DriftX;
            public double DriftY;
            public double DriftVelocityX;
            public double DriftVelocityY;
        }

        //ピン留め頂点バッファ（GPUへのポインタ渡し用。再確保するとポインタが変わるため、Buildの戻り値を毎回使うこと）
        byte[] vertexData = GC.AllocateArray<byte>(ParticleOutputCustomEffect.InitialVertexBufferByteSize, pinned: true);

        //累積テーブル。インデックスは放出クロックのフレーム番号ef（te = ef/fps）。
        //cumEmit[ef] = 時刻ef/fpsまでの累積発生数、driftV*PerFrame[ef] = そのフレームの風・重力の終端速度(px/s)
        readonly List<double> cumEmit = [0];
        readonly List<double> ratePerFrame = [];
        readonly List<double> driftVxPerFrame = [];
        readonly List<double> driftVyPerFrame = [];
        readonly List<double> curlStrengthPerFrame = [];
        readonly List<double> curlScalePerFrame = [];
        readonly List<double> curlSpeedPerFrame = [];
        readonly List<double> cumCurlPhase = [0];

        //発生時に確定する属性のキャッシュ（キー：粒子の通し番号）。フィンガープリントが変わったら破棄する
        readonly Dictionary<long, Attr> attrCache = [];
        const int AttrCacheLimit = 1 << 17;

        //移流キャッシュは生存粒子だけをcurrent/next間で引き継ぎ、死亡粒子を自然に追い出す。
        Dictionary<long, AdvectionState> advectionCurrentCache = [];
        Dictionary<long, AdvectionState> advectionNextCache = [];
        bool hasAdvectionBounds;
        RawRectF advectionBounds;

        int tableFps;
        long tableLength;
        int tablePrerollFrames;
        int tableSeed;
        int fingerprint;
        bool hasFingerprint;

        readonly List<(long Key, Attr Attr)> aliveBuffer = [];

        /// <summary>
        /// 時刻te（事前再生込みの放出クロック、秒）の頂点データを構築する。
        /// </summary>
        public Result Build(ParticleOutputEffect item, double te, int fps, long lengthFrames, int prerollFrames, int seed, RawRectF bounds)
        {
            fps = Math.Max(1, fps);
            lengthFrames = Math.Max(1, lengthFrames);

            InvalidateIfChanged(item, fps, lengthFrames, prerollFrames, seed);

            var boundsCenter = new Vector2((bounds.Left + bounds.Right) * 0.5f, (bounds.Top + bounds.Bottom) * 0.5f);
            var boundsHalf = new Vector2(
                MathF.Max(0, (bounds.Right - bounds.Left) * 0.5f),
                MathF.Max(0, (bounds.Bottom - bounds.Top) * 0.5f));
            var cornerBase = boundsHalf.Length();

            //生存中の粒子を列挙する
            aliveBuffer.Clear();
            if (te > 0)
            {
                EnsureTables(item, (int)Math.Ceiling(te * fps) + 1);
                var totalBirths = (long)Math.Floor(GetCumulativeEmission(te));
                for (var k = totalBirths - 1; k >= 0 && aliveBuffer.Count < ParticleOutputCustomEffect.MaxParticles; k--)
                {
                    var birth = GetBirthTime(k);
                    var age = te - birth;
                    if (age >= MaxLifetimeSeconds)
                        break;//これより古い粒子はすべて寿命切れ（発生時刻はkについて単調）
                    if (age < 0)
                        continue;//数値誤差の保険
                    var attr = GetAttr(item, k);
                    if (age < attr.Lifetime)
                        aliveBuffer.Add((k, attr));
                }
                //古い粒子から順に描画する（新しい粒子が上に重なる）
                aliveBuffer.Reverse();
            }

            var currentFrame = (long)Math.Round(te * fps);
            var perspective = Math.Clamp(SampleAt(item.Perspective, currentFrame), 0, 1000);
            var curlStrength = Math.Clamp(SampleAt(item.CurlStrength, currentFrame), 0, YMM4Constants.VeryLargeValue);
            var curlScale = Math.Clamp(SampleAt(item.CurlScale, currentFrame), 1, YMM4Constants.VeryLargeValue);
            var curlPhase = GetCumulativeCurlPhase(te);
            var focalLength = perspective > 0 ? (float)(100000 / perspective) : 0f;
            var nearDenominator = perspective > 0 ? MathF.Max(1, focalLength * 0.05f) : 0f;
            var isAdvection = item.CurlType == ParticleOutputCurlType.Advection;
            var hasAdvectionActivity = isAdvection && HasAdvectionActivity(te);

            //Z運動とcurlが無ければ、遠近感の値にかかわらず投影は恒等なので旧2D経路を通す。
            //旧計算順を維持し、既定値goldenのbit一致を保証する。
            //移流は現在の強さが0でも過去のoffsetが残るため、生存期間の履歴全体が0の場合だけ旧経路へ戻す。
            var legacy2D = isAdvection ? !hasAdvectionActivity : curlStrength == 0;
            if (legacy2D)
                for (var i = 0; i < aliveBuffer.Count; i++)
                    if (aliveBuffer[i].Attr.OriginZ != 0 || aliveBuffer[i].Attr.VelocityZ != 0)
                    {
                        legacy2D = false;
                        break;
                    }

            if (legacy2D)
                return BuildLegacy2D();

            //3D経路はcurl後の状態をscratchへ一度だけ計算し、可視粒子を最終Z降順・Key昇順で並べる。
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            var visibleCount = 0;
            var allDepthsEqual = true;
            var firstDepth = 0f;
            var count = aliveBuffer.Count;
            if (count > 0)
            {
                EnsureScratchCapacity(count);
                //流れタイプは履歴強さが0でも、3D基底運動があればゼロ移流状態としてこの経路を通す。
                //うねり経路へ落とすと、境界直前にRoundされた現在値のcurl変位を先取りしてしまう。
                if (isAdvection)
                {
                    InvalidateAdvectionIfBoundsChanged(bounds);
                    PrepareAdvectionStartStates(count);
                    if (count >= ParallelThreshold)
                        Parallel.For(0, count, ComputeAdvectionParticleState);
                    else
                        for (var i = 0; i < count; i++)
                            ComputeAdvectionParticleState(i);
                    CommitAdvectionStates(count);
                }
                else if (count >= ParallelThreshold)
                    Parallel.For(0, count, ComputeDisplacementParticleState);
                else
                    for (var i = 0; i < count; i++)
                        ComputeDisplacementParticleState(i);

                for (var i = 0; i < count; i++)
                {
                    if (!scratchVisible[i])
                        continue;
                    if (visibleCount == 0)
                        firstDepth = scratchZ[i];
                    else if (scratchZ[i] != firstDepth)
                        allDepthsEqual = false;
                    sortEntries[visibleCount++] = new SortEntry(scratchZ[i], aliveBuffer[i].Key, i, ToDescendingSortKey(scratchZ[i]));
                }
                //同一ZならaliveBufferのKey昇順がそのまま正しいため、比較ソートを省略する。
                if (!allDepthsEqual)
                    StableRadixSort(sortEntries, sortEntriesBuffer, visibleCount);
            }

            var particleCount = Math.Max(1, visibleCount);
            var byteCount = particleCount * 6 * VertexStride;
            EnsureVertexDataCapacity(byteCount);

            if (visibleCount == 0)
            {
                WriteDummy(byteCount);
            }
            else
            {
                if (visibleCount >= ParallelThreshold)
                    Parallel.For(0, visibleCount, WriteSortedParticle);
                else
                    for (var i = 0; i < visibleCount; i++)
                        WriteSortedParticle(i);

                for (var i = 0; i < visibleCount; i++)
                {
                    var sourceIndex = sortEntries[i].SourceIndex;
                    minX = MathF.Min(minX, scratchX[sourceIndex] - scratchRadius[sourceIndex]);
                    minY = MathF.Min(minY, scratchY[sourceIndex] - scratchRadius[sourceIndex]);
                    maxX = MathF.Max(maxX, scratchX[sourceIndex] + scratchRadius[sourceIndex]);
                    maxY = MathF.Max(maxY, scratchY[sourceIndex] + scratchRadius[sourceIndex]);
                }
            }

            return new Result(
                Marshal.UnsafeAddrOfPinnedArrayElement(vertexData, 0),
                byteCount,
                particleCount * 6,
                visibleCount > 0,
                minX,
                minY,
                maxX,
                maxY,
                visibleCount > 0 ? focalLength : 0,
                visibleCount > 0 ? nearDenominator : 0);

            void ComputeDisplacementParticleState(int i)
            {
                var attr = aliveBuffer[i].Attr;
                var tau = (float)(te - attr.Birth);
                var (driftXValue, driftYValue) = ComputeDriftDisplacement(attr.Birth, te, 1.5 / attr.Lifetime);
                var driftX = (float)driftXValue;
                var driftY = (float)driftYValue;
                var lifetimeInv = (float)(1 / attr.Lifetime);
                var progress = Math.Clamp(tau * lifetimeInv, 0, 1);
                var decay = 1.5f * lifetimeInv;
                var tauD = (1 - MathF.Exp(-decay * tau)) / decay;
                var swirl = attr.Swirl * tauD;
                var (swirlSin, swirlCos) = MathF.SinCos(swirl);
                var worldX = boundsCenter.X + attr.OriginOffsetX
                    + (attr.VelocityX * swirlCos - attr.VelocityY * swirlSin) * tauD
                    + driftX;
                var worldY = boundsCenter.Y + attr.OriginOffsetY
                    + (attr.VelocityX * swirlSin + attr.VelocityY * swirlCos) * tauD
                    + driftY;
                var worldZ = attr.OriginZ + attr.VelocityZ * tauD;

                if (curlStrength != 0)
                {
                    var phase = (float)curlPhase;
                    var q = new Vector3(
                        worldX / (float)curlScale + phase * 0.7548777f,
                        worldY / (float)curlScale + phase * 0.5698403f,
                        worldZ / (float)curlScale + phase * 0.4382891f);
                    var envelopeX = Math.Clamp(progress / 0.15f, 0, 1);
                    var envelope = envelopeX * envelopeX * (3 - 2 * envelopeX);
                    var curl = ParticleOutputCurlNoise.EvaluateCurl(q) * ((float)curlStrength * envelope);
                    if (float.IsFinite(curl.X) && float.IsFinite(curl.Y) && float.IsFinite(curl.Z))
                    {
                        driftX += curl.X;
                        driftY += curl.Y;
                        worldX += curl.X;
                        worldY += curl.Y;
                        worldZ += curl.Z;
                    }
                }

                if (!float.IsFinite(worldZ))
                    worldZ = 0;
                else if (worldZ == 0)
                    worldZ = 0;//-0を+0へ正規化し、同一ZのKey順を保つ
                var radius = cornerBase * attr.SizeScale * float.Lerp(1, attr.EndScale, progress);
                scratchTau[i] = tau;
                scratchDriftX[i] = driftX;
                scratchDriftY[i] = driftY;
                scratchZ[i] = worldZ;
                scratchVisible[i] = TryProject(
                    worldX, worldY, worldZ, radius, boundsCenter,
                    focalLength, nearDenominator,
                    out scratchX[i], out scratchY[i], out scratchRadius[i]);
            }

            void ComputeAdvectionParticleState(int i)
            {
                var attr = aliveBuffer[i].Attr;
                var result = ComputeAdvection(
                    attr,
                    scratchHasAdvectionStart[i],
                    scratchAdvectionStart[i],
                    te,
                    boundsCenter);
                scratchAdvectionResult[i] = result;

                var tau = (float)(te - attr.Birth);
                var lifetimeInv = (float)(1 / attr.Lifetime);
                var progress = Math.Clamp(tau * lifetimeInv, 0, 1);
                var basePosition = ComputeBasePosition(attr, te, result.DriftX, result.DriftY, boundsCenter);
                var world = basePosition + result.CurlOffset;
                var driftX = (float)result.DriftX + result.CurlOffset.X;
                var driftY = (float)result.DriftY + result.CurlOffset.Y;
                var worldZ = world.Z;

                if (!float.IsFinite(worldZ))
                    worldZ = 0;
                else if (worldZ == 0)
                    worldZ = 0;//-0を+0へ正規化し、同一ZのKey順を保つ
                var radius = cornerBase * attr.SizeScale * float.Lerp(1, attr.EndScale, progress);
                scratchTau[i] = tau;
                scratchDriftX[i] = driftX;
                scratchDriftY[i] = driftY;
                scratchZ[i] = worldZ;
                scratchVisible[i] = TryProject(
                    world.X, world.Y, worldZ, radius, boundsCenter,
                    focalLength, nearDenominator,
                    out scratchX[i], out scratchY[i], out scratchRadius[i]);
            }

            void WriteSortedParticle(int slot)
            {
                var sourceIndex = sortEntries[slot].SourceIndex;
                var attr = aliveBuffer[sourceIndex].Attr;
                var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(slot * 6 * VertexStride, 6 * VertexStride));
                var fi = 0;
                WriteParticle(floats, ref fi, attr, -scratchTau[sourceIndex], scratchZ[sourceIndex], scratchDriftX[sourceIndex], scratchDriftY[sourceIndex]);
            }

            Result BuildLegacy2D()
            {
                //以下は3D対応前の計算順を維持し、既定値goldenのbit一致を保つ。
                var legacyParticleCount = Math.Max(1, aliveBuffer.Count);
                var legacyByteCount = legacyParticleCount * 6 * VertexStride;
                EnsureVertexDataCapacity(legacyByteCount);
                var legacyMinX = float.MaxValue;
                var legacyMinY = float.MaxValue;
                var legacyMaxX = float.MinValue;
                var legacyMaxY = float.MinValue;

                if (aliveBuffer.Count == 0)
                {
                    WriteDummy(legacyByteCount);
                }
                else
                {
                    var legacyCount = aliveBuffer.Count;
                    EnsureScratchCapacity(legacyCount);
                    if (legacyCount >= ParallelThreshold)
                        Parallel.For(0, legacyCount, ProcessLegacyParticle);
                    else
                        for (var i = 0; i < legacyCount; i++)
                            ProcessLegacyParticle(i);

                    for (var i = 0; i < legacyCount; i++)
                    {
                        legacyMinX = MathF.Min(legacyMinX, scratchX[i] - scratchRadius[i]);
                        legacyMinY = MathF.Min(legacyMinY, scratchY[i] - scratchRadius[i]);
                        legacyMaxX = MathF.Max(legacyMaxX, scratchX[i] + scratchRadius[i]);
                        legacyMaxY = MathF.Max(legacyMaxY, scratchY[i] + scratchRadius[i]);
                    }
                }

                return new Result(
                    Marshal.UnsafeAddrOfPinnedArrayElement(vertexData, 0),
                    legacyByteCount,
                    legacyParticleCount * 6,
                    aliveBuffer.Count > 0,
                    legacyMinX,
                    legacyMinY,
                    legacyMaxX,
                    legacyMaxY,
                    0,
                    0);

                void ProcessLegacyParticle(int i)
                {
                    var attr = aliveBuffer[i].Attr;
                    var tau = (float)(te - attr.Birth);
                    var (driftX, driftY) = ComputeDriftDisplacement(attr.Birth, te, 1.5 / attr.Lifetime);

                    var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(i * 6 * VertexStride, 6 * VertexStride));
                    var fi = 0;
                    WriteParticle(floats, ref fi, attr, -tau, 0, (float)driftX, (float)driftY);

                    //出力範囲AABB：シェーダーと同じ式で現在位置と粒子半径を求める
                    var lifetimeInv = (float)(1 / attr.Lifetime);
                    var progress = Math.Clamp(tau * lifetimeInv, 0, 1);
                    var decay = 1.5f * lifetimeInv;
                    var tauD = (1 - MathF.Exp(-decay * tau)) / decay;
                    var swirl = attr.Swirl * tauD;
                    var (swirlSin, swirlCos) = MathF.SinCos(swirl);
                    scratchX[i] = boundsCenter.X + attr.OriginOffsetX
                        + (attr.VelocityX * swirlCos - attr.VelocityY * swirlSin) * tauD
                        + (float)driftX;
                    scratchY[i] = boundsCenter.Y + attr.OriginOffsetY
                        + (attr.VelocityX * swirlSin + attr.VelocityY * swirlCos) * tauD
                        + (float)driftY;
                    scratchRadius[i] = cornerBase * attr.SizeScale * float.Lerp(1, attr.EndScale, progress);
                }
            }

            void WriteDummy(int dummyByteCount)
            {
                //面積0のダミー1粒子。運動計算に有限値を渡し、Z=0・投影無効で空描画にする。
                var dummy = new Attr(
                    Birth: 0, Lifetime: 1,
                    OriginOffsetX: 0, OriginOffsetY: 0, OriginZ: 0,
                    VelocityX: 0, VelocityY: 0, VelocityZ: 0,
                    Swirl: 0, RotationSpeed: 0, SizeScale: 0, EndScale: 1, Fade: 0);
                var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(0, dummyByteCount));
                var fi = 0;
                WriteParticle(floats, ref fi, dummy, 0, 0, 0, 0);
            }
        }

        bool HasAdvectionActivity(double te)
        {
            if (aliveBuffer.Count == 0 || curlStrengthPerFrame.Count == 0)
                return false;

            var firstFrame = Math.Clamp(FloorBoundaryFrame(aliveBuffer[0].Attr.Birth), 0, curlStrengthPerFrame.Count - 1);
            var lastFrame = Math.Clamp(FloorBoundaryFrame(te), firstFrame, curlStrengthPerFrame.Count - 1);
            for (var frame = firstFrame; frame <= lastFrame; frame++)
                if (curlStrengthPerFrame[(int)frame] != 0)
                    return true;
            return false;
        }

        void InvalidateAdvectionIfBoundsChanged(RawRectF bounds)
        {
            if (hasAdvectionBounds && BoundsNearlyEqual(advectionBounds, bounds))
                return;

            advectionCurrentCache.Clear();
            advectionNextCache.Clear();
            advectionBounds = bounds;
            hasAdvectionBounds = true;
        }

        internal static bool BoundsNearlyEqual(RawRectF x, RawRectF y)
            => MathF.Abs(x.Left - y.Left) < 0.5f
            && MathF.Abs(x.Top - y.Top) < 0.5f
            && MathF.Abs(x.Right - y.Right) < 0.5f
            && MathF.Abs(x.Bottom - y.Bottom) < 0.5f;

        void PrepareAdvectionStartStates(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var hasState = advectionCurrentCache.TryGetValue(aliveBuffer[i].Key, out var state);
                scratchHasAdvectionStart[i] = hasState;
                scratchAdvectionStart[i] = state;
            }
        }

        void CommitAdvectionStates(int count)
        {
            advectionNextCache.Clear();
            for (var i = 0; i < count; i++)
            {
                var result = scratchAdvectionResult[i];
                if (result.HasBoundaryState)
                    advectionNextCache[aliveBuffer[i].Key] = result.BoundaryState;
            }
            (advectionCurrentCache, advectionNextCache) = (advectionNextCache, advectionCurrentCache);
        }

        AdvectionResult ComputeAdvection(in Attr attr, bool hasCachedState, in AdvectionState cachedState, double te, Vector2 boundsCenter)
        {
            var targetBoundaryFrame = FloorBoundaryFrame(te);
            var useCachedState = hasCachedState
                && cachedState.BoundaryFrame <= targetBoundaryFrame
                && BoundaryTime(cachedState.BoundaryFrame) >= attr.Birth - 1e-12;

            AdvectionIntegrationState state;
            long boundaryFrame;
            double stateTime;
            var hasBoundaryState = false;

            if (useCachedState)
            {
                state = new AdvectionIntegrationState
                {
                    CurlOffset = cachedState.CurlOffset,
                    DriftX = cachedState.DriftX,
                    DriftY = cachedState.DriftY,
                    DriftVelocityX = cachedState.DriftVelocityX,
                    DriftVelocityY = cachedState.DriftVelocityY,
                };
                boundaryFrame = cachedState.BoundaryFrame;
                stateTime = BoundaryTime(boundaryFrame);
                hasBoundaryState = true;
            }
            else
            {
                state = default;
                stateTime = attr.Birth;
                boundaryFrame = CeilingBoundaryFrame(attr.Birth);
                if (boundaryFrame <= targetBoundaryFrame)
                {
                    var boundaryTime = BoundaryTime(boundaryFrame);
                    if (boundaryTime > stateTime)
                        AdvanceAdvectionSegment(ref state, attr, stateTime, boundaryTime, FloorBoundaryFrame(stateTime), boundsCenter);
                    stateTime = boundaryTime;
                    hasBoundaryState = true;
                }
            }

            if (hasBoundaryState)
            {
                while (boundaryFrame < targetBoundaryFrame)
                {
                    var nextBoundaryFrame = boundaryFrame + 1;
                    var nextTime = BoundaryTime(nextBoundaryFrame);
                    AdvanceAdvectionSegment(ref state, attr, stateTime, nextTime, boundaryFrame, boundsCenter);
                    boundaryFrame = nextBoundaryFrame;
                    stateTime = nextTime;
                }
            }

            var boundaryState = hasBoundaryState
                ? new AdvectionState(
                    boundaryFrame,
                    state.CurlOffset,
                    state.DriftX,
                    state.DriftY,
                    state.DriftVelocityX,
                    state.DriftVelocityY)
                : default;

            var outputState = state;
            if (te > stateTime)
            {
                //整数境界からの端数は、double時刻をframeへ戻さず保持済みの境界番号を左端値に使う。
                //出生から最初の端数だけは出生側のフレーム番号を使う。
                var outputParameterFrame = hasBoundaryState ? boundaryFrame : FloorBoundaryFrame(attr.Birth);
                AdvanceAdvectionSegment(ref outputState, attr, stateTime, te, outputParameterFrame, boundsCenter);
            }

            return new AdvectionResult(
                outputState.CurlOffset,
                outputState.DriftX,
                outputState.DriftY,
                hasBoundaryState,
                boundaryState);
        }

        void AdvanceAdvectionSegment(
            ref AdvectionIntegrationState state,
            in Attr attr,
            double startTime,
            double endTime,
            long parameterFrame,
            Vector2 boundsCenter)
        {
            var dt = endTime - startTime;
            if (dt <= 0)
                return;

            var tableIndex = (int)Math.Clamp(parameterFrame, 0, curlStrengthPerFrame.Count - 1);
            var curlStrength = curlStrengthPerFrame[tableIndex];
            if (curlStrength != 0 && double.IsFinite(curlStrength))
            {
                var basePosition = ComputeBasePosition(attr, startTime, state.DriftX, state.DriftY, boundsCenter);
                var samplePosition = basePosition + state.CurlOffset;
                var curlScale = curlScalePerFrame[tableIndex];
                var phase = (float)GetCumulativeCurlPhase(startTime);
                if (IsFinite(samplePosition) && double.IsFinite(curlScale) && curlScale > 0 && float.IsFinite(phase))
                {
                    var q = new Vector3(
                        samplePosition.X / (float)curlScale + phase * 0.7548777f,
                        samplePosition.Y / (float)curlScale + phase * 0.5698403f,
                        samplePosition.Z / (float)curlScale + phase * 0.4382891f);
                    if (IsFinite(q))
                    {
                        var curl = ParticleOutputCurlNoise.EvaluateCurl(q);
                        if (IsFinite(curl))
                        {
                            var candidate = state.CurlOffset + curl * ((float)curlStrength * (float)dt);
                            if (IsFinite(candidate))
                                state.CurlOffset = candidate;
                        }
                    }
                }
            }

            var driftVx = double.IsFinite(driftVxPerFrame[tableIndex]) ? driftVxPerFrame[tableIndex] : 0;
            var driftVy = double.IsFinite(driftVyPerFrame[tableIndex]) ? driftVyPerFrame[tableIndex] : 0;
            var decay = 1.5 / attr.Lifetime;
            var decayFactor = Math.Exp(-decay * dt);
            var responseWeight = (1 - decayFactor) / decay;
            var previousVelocityX = state.DriftVelocityX;
            var previousVelocityY = state.DriftVelocityY;
            state.DriftX += driftVx * dt + (previousVelocityX - driftVx) * responseWeight;
            state.DriftY += driftVy * dt + (previousVelocityY - driftVy) * responseWeight;
            state.DriftVelocityX = driftVx + (previousVelocityX - driftVx) * decayFactor;
            state.DriftVelocityY = driftVy + (previousVelocityY - driftVy) * decayFactor;
        }

        static Vector3 ComputeBasePosition(in Attr attr, double time, double driftX, double driftY, Vector2 boundsCenter)
        {
            var tau = (float)(time - attr.Birth);
            var lifetimeInv = (float)(1 / attr.Lifetime);
            var decay = 1.5f * lifetimeInv;
            var tauD = (1 - MathF.Exp(-decay * tau)) / decay;
            var swirl = attr.Swirl * tauD;
            var (swirlSin, swirlCos) = MathF.SinCos(swirl);
            return new Vector3(
                boundsCenter.X + attr.OriginOffsetX
                    + (attr.VelocityX * swirlCos - attr.VelocityY * swirlSin) * tauD
                    + (float)driftX,
                boundsCenter.Y + attr.OriginOffsetY
                    + (attr.VelocityX * swirlSin + attr.VelocityY * swirlCos) * tauD
                    + (float)driftY,
                attr.OriginZ + attr.VelocityZ * tauD);
        }

        long FloorBoundaryFrame(double time)
            => (long)Math.Floor(time * tableFps);

        long CeilingBoundaryFrame(double time)
            => (long)Math.Ceiling(time * tableFps);

        double BoundaryTime(long frame)
            => (double)frame / tableFps;

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        internal static bool TryProject(
            float worldX, float worldY, float worldZ, float radius, Vector2 center,
            float focalLength, float nearDenominator,
            out float projectedX, out float projectedY, out float projectedRadius)
        {
            if (focalLength <= 0)
            {
                projectedX = worldX;
                projectedY = worldY;
                projectedRadius = radius;
                return float.IsFinite(worldX) && float.IsFinite(worldY) && float.IsFinite(radius);
            }

            var denominator = focalLength + worldZ;
            if (!float.IsFinite(denominator) || denominator <= nearDenominator)
            {
                projectedX = projectedY = projectedRadius = 0;
                return false;
            }
            var projection = MathF.Min(20, focalLength / denominator);
            projectedX = center.X + (worldX - center.X) * projection;
            projectedY = center.Y + (worldY - center.Y) * projection;
            projectedRadius = MathF.Abs(radius * projection);
            return float.IsFinite(projectedX) && float.IsFinite(projectedY) && float.IsFinite(projectedRadius);
        }

        static void WriteParticle(Span<float> floats, ref int fi, in Attr attr, float birthRel, float currentZ, float driftDispX, float driftDispY)
        {
            //三角形 (左上,右上,右下) と (左上,右下,左下)
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, currentZ, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, -1, attr, birthRel, currentZ, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, currentZ, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, currentZ, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, currentZ, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, -1, +1, attr, birthRel, currentZ, driftDispX, driftDispY);
        }

        static void WriteVertex(Span<float> floats, ref int fi, float cornerX, float cornerY, in Attr attr, float birthRel, float currentZ, float driftDispX, float driftDispY)
        {
            //レイアウトはParticleOutputVertex.hlslのVSIn・ParticleOutputCustomEffectのInputElementDescriptionと一致させること
            floats[fi++] = cornerX;
            floats[fi++] = cornerY;
            floats[fi++] = birthRel;
            floats[fi++] = attr.SizeScale;
            floats[fi++] = attr.OriginOffsetX;
            floats[fi++] = attr.OriginOffsetY;
            floats[fi++] = attr.VelocityX;
            floats[fi++] = attr.VelocityY;
            floats[fi++] = attr.Swirl;
            floats[fi++] = attr.RotationSpeed;
            floats[fi++] = (float)(1 / attr.Lifetime);
            floats[fi++] = attr.EndScale;
            floats[fi++] = attr.Fade;
            floats[fi++] = currentZ;
            floats[fi++] = driftDispX;
            floats[fi++] = driftDispY;
        }

        //粒子ごとのドリフト積分は寿命分のフレーム走査を伴うため、粒子数がこの値以上なら並列化する
        const int ParallelThreshold = 1024;

        float[] scratchX = [];
        float[] scratchY = [];
        float[] scratchRadius = [];
        float[] scratchTau = [];
        float[] scratchDriftX = [];
        float[] scratchDriftY = [];
        float[] scratchZ = [];
        bool[] scratchVisible = [];
        bool[] scratchHasAdvectionStart = [];
        AdvectionState[] scratchAdvectionStart = [];
        AdvectionResult[] scratchAdvectionResult = [];
        SortEntry[] sortEntries = [];
        SortEntry[] sortEntriesBuffer = [];

        readonly record struct SortEntry(float Depth, long Key, int SourceIndex, uint SortKey);

        static uint ToDescendingSortKey(float depth)
        {
            var bits = BitConverter.SingleToUInt32Bits(depth);
            var ascending = (bits & 0x80000000) != 0 ? ~bits : bits ^ 0x80000000;
            return ~ascending;
        }

        static void StableRadixSort(SortEntry[] entries, SortEntry[] buffer, int count)
        {
            //aliveBufferはKey昇順なので、stable sortなら同一ZのKey昇順もそのまま維持される。
            var source = entries;
            var destination = buffer;
            Span<int> counts = stackalloc int[256];
            Span<int> offsets = stackalloc int[256];
            for (var shift = 0; shift < 32; shift += 8)
            {
                counts.Clear();
                for (var i = 0; i < count; i++)
                    counts[(int)((source[i].SortKey >> shift) & 0xFF)]++;
                var offset = 0;
                for (var i = 0; i < counts.Length; i++)
                {
                    offsets[i] = offset;
                    offset += counts[i];
                }
                for (var i = 0; i < count; i++)
                {
                    var entry = source[i];
                    destination[offsets[(int)((entry.SortKey >> shift) & 0xFF)]++] = entry;
                }
                (source, destination) = (destination, source);
            }
            //4 passなので最終結果はentriesへ戻る。
        }

        void EnsureScratchCapacity(int count)
        {
            if (scratchX.Length >= count)
                return;
            var newSize = Math.Max(1024, scratchX.Length);
            while (newSize < count)
                newSize *= 2;
            scratchX = new float[newSize];
            scratchY = new float[newSize];
            scratchRadius = new float[newSize];
            scratchTau = new float[newSize];
            scratchDriftX = new float[newSize];
            scratchDriftY = new float[newSize];
            scratchZ = new float[newSize];
            scratchVisible = new bool[newSize];
            scratchHasAdvectionStart = new bool[newSize];
            scratchAdvectionStart = new AdvectionState[newSize];
            scratchAdvectionResult = new AdvectionResult[newSize];
            sortEntries = new SortEntry[newSize];
            sortEntriesBuffer = new SortEntry[newSize];
        }

        /// <summary>
        /// 風・重力による粒子の変位(px)を計算する。
        /// 線形抵抗 v' = -k(v - v_terminal) の解より、変位 = ∫ v_terminal(s) × (1 - e^(-k(te-s))) ds （s: 発生時刻から現在まで）。
        /// 終端速度はフレーム内で一定（テーブルの値）として、セグメントごとに解析的に積分する。
        /// 風・重力が一定なら v × (tau - tauD) となり、従来の閉形式と厳密に一致する。
        /// </summary>
        (double X, double Y) ComputeDriftDisplacement(double birth, double te, double k)
        {
            var efMax = driftVxPerFrame.Count - 1;
            if (efMax < 0 || te <= birth)
                return (0, 0);

            var fps = tableFps;
            var efTop = Math.Clamp((int)Math.Floor(te * fps), 0, efMax);
            var efBirth = Math.Clamp((int)Math.Floor(birth * fps), 0, efTop);
            var stepFactor = Math.Exp(-k / fps);

            double dispX = 0, dispY = 0;
            var e1 = 1.0;//セグメント終端の減衰係数 e^(-k(te-s1))。最新セグメントの終端はteなので1
            for (var ef = efTop; ef >= efBirth; ef--)
            {
                var frameStart = (double)ef / fps;
                var s0 = Math.Max(birth, frameStart);
                var s1 = ef == efTop ? te : (double)(ef + 1) / fps;
                var isFullStep = ef != efTop && s0 == frameStart;
                var e0 = e1 * (isFullStep ? stepFactor : Math.Exp(-k * (s1 - s0)));
                var weight = (s1 - s0) - (e1 - e0) / k;
                dispX += driftVxPerFrame[ef] * weight;
                dispY += driftVyPerFrame[ef] * weight;
                e1 = e0;
            }
            return (dispX, dispY);
        }

        void EnsureVertexDataCapacity(int byteCount)
        {
            if (vertexData.Length >= byteCount)
                return;
            var newSize = vertexData.Length;
            while (newSize < byteCount)
                newSize *= 2;
            vertexData = GC.AllocateArray<byte>(newSize, pinned: true);
        }

        /// <summary>
        /// アニメーション・時間設定が変わっていたらテーブルとキャッシュを破棄する。
        /// AnimationはPropertyChangedの購読で内部の全変更を検知できないため、内容のフィンガープリントで比較する。
        /// </summary>
        void InvalidateIfChanged(ParticleOutputEffect item, int fps, long lengthFrames, int prerollFrames, int seed)
        {
            var hash = new HashCode();
            hash.Add(fps);
            hash.Add(lengthFrames);
            hash.Add(prerollFrames);
            hash.Add(seed);
            AddAnimation(ref hash, item.Rate);
            AddAnimation(ref hash, item.Lifetime);
            AddAnimation(ref hash, item.X);
            AddAnimation(ref hash, item.Y);
            AddAnimation(ref hash, item.Z);
            AddAnimation(ref hash, item.Size);
            AddAnimation(ref hash, item.EmitRange);
            AddAnimation(ref hash, item.Randomness);
            AddAnimation(ref hash, item.EmitAngle);
            AddAnimation(ref hash, item.EmitElevation);
            AddAnimation(ref hash, item.SpreadAngle);
            AddAnimation(ref hash, item.ElevationSpreadAngle);
            AddAnimation(ref hash, item.Speed);
            AddAnimation(ref hash, item.Perspective);
            AddAnimation(ref hash, item.Gravity);
            AddAnimation(ref hash, item.WindAngle);
            AddAnimation(ref hash, item.WindSpeed);
            AddAnimation(ref hash, item.Turbulence);
            hash.Add((int)item.CurlType);
            AddAnimation(ref hash, item.CurlStrength);
            AddAnimation(ref hash, item.CurlScale);
            AddAnimation(ref hash, item.CurlSpeed);
            AddAnimation(ref hash, item.Rotation);
            AddAnimation(ref hash, item.EndScale);
            AddAnimation(ref hash, item.Fade);
            var newFingerprint = hash.ToHashCode();

            if (hasFingerprint && fingerprint == newFingerprint)
                return;

            fingerprint = newFingerprint;
            hasFingerprint = true;
            tableFps = fps;
            tableLength = lengthFrames;
            tablePrerollFrames = prerollFrames;
            tableSeed = seed;

            cumEmit.Clear();
            cumEmit.Add(0);
            ratePerFrame.Clear();
            driftVxPerFrame.Clear();
            driftVyPerFrame.Clear();
            curlStrengthPerFrame.Clear();
            curlScalePerFrame.Clear();
            curlSpeedPerFrame.Clear();
            cumCurlPhase.Clear();
            cumCurlPhase.Add(0);
            attrCache.Clear();
            advectionCurrentCache.Clear();
            advectionNextCache.Clear();
            hasAdvectionBounds = false;
        }

        static void AddAnimation(ref HashCode hash, Animation animation)
        {
            hash.Add((int)animation.AnimationType);
            hash.Add(animation.Span);
            foreach (var value in animation.Values)
                hash.Add(value.Value);
            var keyFrames = animation.KeyFrames;
            hash.Add(keyFrames?.Count ?? -1);
            if (keyFrames is not null)
                foreach (var frame in keyFrames.Frames)
                    hash.Add(frame);
            hash.Add(animation.Bezier.IsQuadratic);
            foreach (var point in animation.Bezier.Points)
            {
                hash.Add(point.Point);
                hash.Add(point.ControlPoint1);
                hash.Add(point.ControlPoint2);
            }
        }

        /// <summary>放出クロックのフレームefをアイテムのフレーム番号に変換する（事前再生中はフレーム0の値を使う）</summary>
        long ToItemFrame(long emissionFrame)
            => Math.Clamp(emissionFrame - tablePrerollFrames, 0, tableLength);

        double SampleAt(Animation animation, long emissionFrame)
            => animation.GetValue(ToItemFrame(emissionFrame), tableLength, tableFps);

        /// <summary>累積テーブルをフレームframeCountまで構築する</summary>
        void EnsureTables(ParticleOutputEffect item, int frameCount)
        {
            for (var ef = ratePerFrame.Count; ef < frameCount; ef++)
            {
                var rate = Math.Clamp(SampleAt(item.Rate, ef), 0, 2000);
                ratePerFrame.Add(rate);
                cumEmit.Add(cumEmit[ef] + rate / tableFps);

                var windAngle = SampleAt(item.WindAngle, ef) * Math.PI / 180;
                var windSpeed = Math.Clamp(SampleAt(item.WindSpeed, ef), 0, YMM4Constants.VeryLargeValue);
                var gravity = Math.Clamp(
                    SampleAt(item.Gravity, ef),
                    YMM4Constants.VerySmallValue,
                    YMM4Constants.VeryLargeValue);
                driftVxPerFrame.Add(Math.Cos(windAngle) * windSpeed);
                driftVyPerFrame.Add(Math.Sin(windAngle) * windSpeed + gravity);

                var curlStrength = Math.Clamp(SampleAt(item.CurlStrength, ef), 0, YMM4Constants.VeryLargeValue);
                curlStrengthPerFrame.Add(double.IsFinite(curlStrength) ? curlStrength : 0);
                var curlScale = Math.Clamp(SampleAt(item.CurlScale, ef), 1, YMM4Constants.VeryLargeValue);
                curlScalePerFrame.Add(double.IsFinite(curlScale) ? curlScale : 1);
                var curlSpeed = Math.Clamp(SampleAt(item.CurlSpeed, ef), 0, YMM4Constants.VeryLargeValue) / 100;
                curlSpeedPerFrame.Add(curlSpeed);
                cumCurlPhase.Add(cumCurlPhase[ef] + curlSpeed / tableFps);
            }
        }

#if DEBUG
        internal double GetCumulativeCurlPhaseForTest(ParticleOutputEffect item, double te, int fps, long lengthFrames, int prerollFrames, int seed)
        {
            InvalidateIfChanged(item, fps, lengthFrames, prerollFrames, seed);
            EnsureTables(item, Math.Max(1, (int)Math.Ceiling(te * fps) + 1));
            return GetCumulativeCurlPhase(te);
        }
#endif

        /// <summary>時刻teまでのcurl移動位相（フレーム内は現在区間の速度で補間）</summary>
        double GetCumulativeCurlPhase(double te)
        {
            if (te <= 0 || curlSpeedPerFrame.Count == 0)
                return 0;
            var ef = Math.Clamp((int)Math.Floor(te * tableFps), 0, curlSpeedPerFrame.Count - 1);
            return cumCurlPhase[ef] + curlSpeedPerFrame[ef] * (te - (double)ef / tableFps);
        }

        /// <summary>時刻teまでの累積発生数（フレーム内は線形補間）</summary>
        double GetCumulativeEmission(double te)
        {
            if (te <= 0 || ratePerFrame.Count == 0)
                return 0;
            var ef = Math.Clamp((int)Math.Floor(te * tableFps), 0, ratePerFrame.Count - 1);
            return cumEmit[ef] + ratePerFrame[ef] * (te - (double)ef / tableFps);
        }

        /// <summary>通し番号kの粒子の発生時刻を累積発生数テーブルから逆引きする</summary>
        double GetBirthTime(long k)
        {
            //cumEmit[ef] <= k < cumEmit[ef+1] となる区間を二分探索（upper bound）で求める
            var lo = 0;
            var hi = cumEmit.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (cumEmit[mid] <= k)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            var ef = Math.Max(0, lo - 1);
            var rate = ratePerFrame[ef];
            var fraction = rate > 0 ? (k - cumEmit[ef]) / rate : 0;
            return (double)ef / tableFps + fraction;
        }

        /// <summary>粒子kの発生時に確定する属性を計算する（キャッシュあり）</summary>
        Attr GetAttr(ParticleOutputEffect item, long k)
        {
            if (attrCache.TryGetValue(k, out var cached))
                return cached;

            var birth = GetBirthTime(k);
            var bf = (long)Math.Round(birth * tableFps);

            var lifetime = Math.Clamp(SampleAt(item.Lifetime, bf), 0.01, MaxLifetimeSeconds);
            var emitX = Math.Clamp(
                SampleAt(item.X, bf),
                YMM4Constants.VerySmallValue,
                YMM4Constants.VeryLargeValue);
            var emitY = Math.Clamp(
                SampleAt(item.Y, bf),
                YMM4Constants.VerySmallValue,
                YMM4Constants.VeryLargeValue);
            var emitZ = Math.Clamp(
                SampleAt(item.Z, bf),
                YMM4Constants.VerySmallValue,
                YMM4Constants.VeryLargeValue);
            var size = Math.Clamp(SampleAt(item.Size, bf), 0.1, YMM4Constants.VeryLargeValue) / 100;
            var emitRange = Math.Clamp(SampleAt(item.EmitRange, bf), 0, YMM4Constants.VeryLargeValue);
            var randomness = Math.Clamp(SampleAt(item.Randomness, bf), 0, 100) / 100;
            var emitAngle = SampleAt(item.EmitAngle, bf) * Math.PI / 180;
            var emitElevation = SampleAt(item.EmitElevation, bf) * Math.PI / 180;
            var spreadHalf = Math.Clamp(SampleAt(item.SpreadAngle, bf), 0, 360) * Math.PI / 180 / 2;
            var elevationSpreadHalf = Math.Clamp(SampleAt(item.ElevationSpreadAngle, bf), 0, 180) * Math.PI / 180 / 2;
            var speed = Math.Clamp(SampleAt(item.Speed, bf), 0, YMM4Constants.VeryLargeValue);
            var turbulence = Math.Clamp(SampleAt(item.Turbulence, bf), 0, YMM4Constants.VeryLargeValue) / 100 * 3;//渦の角速度(rad/s)、100%=3rad/s
            var rotation = Math.Clamp(SampleAt(item.Rotation, bf), 0, YMM4Constants.VeryLargeValue) * Math.PI / 180;
            var endScale = Math.Clamp(SampleAt(item.EndScale, bf), 0, YMM4Constants.VeryLargeValue) / 100;
            var fade = Math.Clamp(SampleAt(item.Fade, bf), 0, 100) / 100;

            var h1 = Rand(k, 1);
            var h2 = Rand(k, 2);
            var h3 = Rand(k, 3);
            var h4 = Rand(k, 4);
            var h5 = Rand(k, 5);
            var h6 = Rand(k, 6);
            var h7 = Rand(k, 7);
            var h8 = Rand(k, 8);

            //初速：射出方向±拡散の半角、速さはばらつきに応じて30%-100%に分散させる
            var direction = emitAngle + (h1 * 2 - 1) * spreadHalf;
            var elevation = emitElevation + (h8 * 2 - 1) * elevationSpreadHalf;
            var speedFactor = double.Lerp(1, 0.3 + 0.7 * h2, randomness);
            var velocity = speed * speedFactor;
            var planarVelocity = Math.Cos(elevation) * velocity;

            var attr = new Attr(
                birth,
                lifetime,
                (float)((h6 * 2 - 1) * emitRange + emitX),
                (float)((h7 * 2 - 1) * emitRange + emitY),
                (float)emitZ,
                (float)(Math.Cos(direction) * planarVelocity),
                (float)(Math.Sin(direction) * planarVelocity),
                (float)(Math.Sin(elevation) * velocity),
                (float)(turbulence * (h4 * 2 - 1)),
                (float)(rotation * (h5 * 2 - 1)),
                (float)(size * double.Lerp(1, 0.5 + h3, randomness)),
                (float)endScale,
                (float)fade);

            //スクラブで無制限に溜まらないよう、上限を超えたら丸ごと捨てる（再計算は安価）
            if (attrCache.Count >= AttrCacheLimit)
                attrCache.Clear();
            attrCache[k] = attr;
            return attr;
        }

        /// <summary>粒子の通し番号とシードから決定論的な乱数[0,1)を作る（splitmix64）</summary>
        double Rand(long k, int salt)
        {
            var x = (ulong)k * 0x9E3779B97F4A7C15UL ^ (uint)tableSeed ^ ((ulong)(uint)salt << 32);
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) / (double)(1UL << 53);
        }
    }
}
