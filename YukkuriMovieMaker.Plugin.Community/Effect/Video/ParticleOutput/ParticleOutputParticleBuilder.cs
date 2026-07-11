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
    /// 粒子の状態は一切保持せず、時刻から決定論的に導出する：
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
            float Bottom);

        /// <summary>粒子1個の発生時に確定する属性</summary>
        readonly record struct Attr(
            double Birth,
            double Lifetime,
            float OriginOffsetX,
            float OriginOffsetY,
            float VelocityX,
            float VelocityY,
            float Swirl,
            float RotationSpeed,
            float SizeScale,
            float EndScale,
            float Fade);

        //ピン留め頂点バッファ（GPUへのポインタ渡し用。再確保するとポインタが変わるため、Buildの戻り値を毎回使うこと）
        byte[] vertexData = GC.AllocateArray<byte>(ParticleOutputCustomEffect.InitialVertexBufferByteSize, pinned: true);

        //累積テーブル。インデックスは放出クロックのフレーム番号ef（te = ef/fps）。
        //cumEmit[ef] = 時刻ef/fpsまでの累積発生数、driftV*PerFrame[ef] = そのフレームの風・重力の終端速度(px/s)
        readonly List<double> cumEmit = [0];
        readonly List<double> ratePerFrame = [];
        readonly List<double> driftVxPerFrame = [];
        readonly List<double> driftVyPerFrame = [];

        //発生時に確定する属性のキャッシュ（キー：粒子の通し番号）。フィンガープリントが変わったら破棄する
        readonly Dictionary<long, Attr> attrCache = [];
        const int AttrCacheLimit = 1 << 17;

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

            //頂点データを書き込む。粒子が1つも無い場合は面積0のダミー1粒子で空描画にする
            var particleCount = Math.Max(1, aliveBuffer.Count);
            var byteCount = particleCount * 6 * VertexStride;
            EnsureVertexDataCapacity(byteCount);

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            if (aliveBuffer.Count == 0)
            {
                //面積0のダミー1粒子で空描画にする。全ゼロだとシェーダーで 1/寿命=0 が 0/0=NaN を生むため、
                //寿命など運動計算に使う属性は有効値にし、サイズ倍率0で描画されないようにする
                var dummy = new Attr(
                    Birth: 0, Lifetime: 1,
                    OriginOffsetX: 0, OriginOffsetY: 0, VelocityX: 0, VelocityY: 0,
                    Swirl: 0, RotationSpeed: 0, SizeScale: 0, EndScale: 1, Fade: 0);
                var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(0, byteCount));
                var fi = 0;
                WriteParticle(floats, ref fi, dummy, 0, 0, 0);
            }
            else
            {
                //粒子ごとに頂点書き込みとAABB要素（現在位置・半径）を計算する。
                //ドリフト積分は粒子ごとに寿命分のフレームを走査するため、粒子数が多い場合は並列化する
                var count = aliveBuffer.Count;
                EnsureScratchCapacity(count);
                if (count >= ParallelThreshold)
                    Parallel.For(0, count, ProcessParticle);
                else
                    for (var i = 0; i < count; i++)
                        ProcessParticle(i);

                for (var i = 0; i < count; i++)
                {
                    minX = MathF.Min(minX, scratchX[i] - scratchRadius[i]);
                    minY = MathF.Min(minY, scratchY[i] - scratchRadius[i]);
                    maxX = MathF.Max(maxX, scratchX[i] + scratchRadius[i]);
                    maxY = MathF.Max(maxY, scratchY[i] + scratchRadius[i]);
                }

                void ProcessParticle(int i)
                {
                    var attr = aliveBuffer[i].Attr;
                    var tau = (float)(te - attr.Birth);
                    var (driftX, driftY) = ComputeDriftDisplacement(attr.Birth, te, 1.5 / attr.Lifetime);

                    var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(i * 6 * VertexStride, 6 * VertexStride));
                    var fi = 0;
                    WriteParticle(floats, ref fi, attr, -tau, (float)driftX, (float)driftY);

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

            return new Result(
                Marshal.UnsafeAddrOfPinnedArrayElement(vertexData, 0),
                byteCount,
                particleCount * 6,
                aliveBuffer.Count > 0,
                minX,
                minY,
                maxX,
                maxY);
        }

        static void WriteParticle(Span<float> floats, ref int fi, in Attr attr, float birthRel, float driftDispX, float driftDispY)
        {
            //三角形 (左上,右上,右下) と (左上,右下,左下)
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, -1, attr, birthRel, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, driftDispX, driftDispY);
            WriteVertex(floats, ref fi, -1, +1, attr, birthRel, driftDispX, driftDispY);
        }

        static void WriteVertex(Span<float> floats, ref int fi, float cornerX, float cornerY, in Attr attr, float birthRel, float driftDispX, float driftDispY)
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
            floats[fi++] = 0;
            floats[fi++] = driftDispX;
            floats[fi++] = driftDispY;
        }

        //粒子ごとのドリフト積分は寿命分のフレーム走査を伴うため、粒子数がこの値以上なら並列化する
        const int ParallelThreshold = 1024;

        float[] scratchX = [];
        float[] scratchY = [];
        float[] scratchRadius = [];

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
            AddAnimation(ref hash, item.Size);
            AddAnimation(ref hash, item.EmitRange);
            AddAnimation(ref hash, item.Randomness);
            AddAnimation(ref hash, item.EmitAngle);
            AddAnimation(ref hash, item.SpreadAngle);
            AddAnimation(ref hash, item.Speed);
            AddAnimation(ref hash, item.Gravity);
            AddAnimation(ref hash, item.WindAngle);
            AddAnimation(ref hash, item.WindSpeed);
            AddAnimation(ref hash, item.Turbulence);
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
            attrCache.Clear();
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
                var windSpeed = Math.Max(0, SampleAt(item.WindSpeed, ef));
                var gravity = SampleAt(item.Gravity, ef);
                driftVxPerFrame.Add(Math.Cos(windAngle) * windSpeed);
                driftVyPerFrame.Add(Math.Sin(windAngle) * windSpeed + gravity);
            }
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
            var emitX = SampleAt(item.X, bf);
            var emitY = SampleAt(item.Y, bf);
            var size = Math.Clamp(SampleAt(item.Size, bf), 0.1, 1000) / 100;
            var emitRange = Math.Clamp(SampleAt(item.EmitRange, bf), 0, 10000);
            var randomness = Math.Clamp(SampleAt(item.Randomness, bf), 0, 100) / 100;
            var emitAngle = SampleAt(item.EmitAngle, bf) * Math.PI / 180;
            var spreadHalf = Math.Clamp(SampleAt(item.SpreadAngle, bf), 0, 360) * Math.PI / 180 / 2;
            var speed = Math.Clamp(SampleAt(item.Speed, bf), 0, 10000);
            var turbulence = Math.Clamp(SampleAt(item.Turbulence, bf), 0, 10000) / 100 * 3;//渦の角速度(rad/s)、100%=3rad/s
            var rotation = Math.Clamp(SampleAt(item.Rotation, bf), 0, 36000) * Math.PI / 180;
            var endScale = Math.Clamp(SampleAt(item.EndScale, bf), 0, 1000) / 100;
            var fade = Math.Clamp(SampleAt(item.Fade, bf), 0, 100) / 100;

            var h1 = Rand(k, 1);
            var h2 = Rand(k, 2);
            var h3 = Rand(k, 3);
            var h4 = Rand(k, 4);
            var h5 = Rand(k, 5);
            var h6 = Rand(k, 6);
            var h7 = Rand(k, 7);

            //初速：射出方向±拡散の半角、速さはばらつきに応じて30%-100%に分散させる
            var direction = emitAngle + (h1 * 2 - 1) * spreadHalf;
            var speedFactor = double.Lerp(1, 0.3 + 0.7 * h2, randomness);
            var velocity = speed * speedFactor;

            var attr = new Attr(
                birth,
                lifetime,
                (float)((h6 * 2 - 1) * emitRange + emitX),
                (float)((h7 * 2 - 1) * emitRange + emitY),
                (float)(Math.Cos(direction) * velocity),
                (float)(Math.Sin(direction) * velocity),
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
