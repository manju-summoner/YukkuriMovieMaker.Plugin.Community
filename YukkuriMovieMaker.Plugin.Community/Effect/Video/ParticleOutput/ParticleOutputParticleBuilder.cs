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
    /// テーブルと属性キャッシュはアニメーションのフィンガープリントで無効化する（編集されたら作り直し）。
    /// 頂点データはピン留めバッファに書き込み、ポインタ渡しでGPUへ直接コピーされる（CrashMeshBuilderと同じ方式）。
    /// 発生時刻・ドリフト積分は「現在時刻からの相対値」で頂点へ焼き込むため、
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
            float Fade,
            double DriftBirthX,
            double DriftBirthY);

        //ピン留め頂点バッファ（GPUへのポインタ渡し用。再確保するとポインタが変わるため、Buildの戻り値を毎回使うこと）
        byte[] vertexData = GC.AllocateArray<byte>(ParticleOutputCustomEffect.InitialVertexBufferByteSize, pinned: true);

        //累積テーブル。インデックスは放出クロックのフレーム番号ef（te = ef/fps）。
        //cumEmit[ef] = 時刻ef/fpsまでの累積発生数、cumDrift[ef] = 風・重力の速度ベクトルの累積積分(px)
        readonly List<double> cumEmit = [0];
        readonly List<double> ratePerFrame = [];
        readonly List<double> cumDriftX = [0];
        readonly List<double> cumDriftY = [0];
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

            var driftNow = GetDrift(te);

            //頂点データを書き込む。粒子が1つも無い場合は面積0のダミー1粒子で空描画にする
            var particleCount = Math.Max(1, aliveBuffer.Count);
            var byteCount = particleCount * 6 * VertexStride;
            EnsureVertexDataCapacity(byteCount);
            var floats = MemoryMarshal.Cast<byte, float>(vertexData.AsSpan(0, byteCount));

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            if (aliveBuffer.Count == 0)
            {
                floats.Clear();
            }
            else
            {
                var fi = 0;
                foreach (var (_, attr) in aliveBuffer)
                {
                    var tau = (float)(te - attr.Birth);
                    var birthRel = -tau;
                    var driftRelX = (float)(attr.DriftBirthX - driftNow.X);
                    var driftRelY = (float)(attr.DriftBirthY - driftNow.Y);

                    WriteParticle(floats, ref fi, attr, birthRel, driftRelX, driftRelY);

                    //出力範囲AABB：シェーダーと同じ式で現在位置と粒子半径を求める
                    var lifetimeInv = (float)(1 / attr.Lifetime);
                    var progress = Math.Clamp(tau * lifetimeInv, 0, 1);
                    var decay = 1.5f * lifetimeInv;
                    var tauD = (1 - MathF.Exp(-decay * tau)) / decay;
                    var tauW = tau - tauD;
                    var swirl = attr.Swirl * tauD;
                    var (swirlSin, swirlCos) = MathF.SinCos(swirl);
                    var driftEase = tau > 1e-4f ? tauW / tau : 0;
                    var x = boundsCenter.X + attr.OriginOffsetX
                        + (attr.VelocityX * swirlCos - attr.VelocityY * swirlSin) * tauD
                        - driftRelX * driftEase;
                    var y = boundsCenter.Y + attr.OriginOffsetY
                        + (attr.VelocityX * swirlSin + attr.VelocityY * swirlCos) * tauD
                        - driftRelY * driftEase;
                    var radius = cornerBase * attr.SizeScale * float.Lerp(1, attr.EndScale, progress);

                    minX = MathF.Min(minX, x - radius);
                    minY = MathF.Min(minY, y - radius);
                    maxX = MathF.Max(maxX, x + radius);
                    maxY = MathF.Max(maxY, y + radius);
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

        static void WriteParticle(Span<float> floats, ref int fi, in Attr attr, float birthRel, float driftRelX, float driftRelY)
        {
            //三角形 (左上,右上,右下) と (左上,右下,左下)
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, driftRelX, driftRelY);
            WriteVertex(floats, ref fi, +1, -1, attr, birthRel, driftRelX, driftRelY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, driftRelX, driftRelY);
            WriteVertex(floats, ref fi, -1, -1, attr, birthRel, driftRelX, driftRelY);
            WriteVertex(floats, ref fi, +1, +1, attr, birthRel, driftRelX, driftRelY);
            WriteVertex(floats, ref fi, -1, +1, attr, birthRel, driftRelX, driftRelY);
        }

        static void WriteVertex(Span<float> floats, ref int fi, float cornerX, float cornerY, in Attr attr, float birthRel, float driftRelX, float driftRelY)
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
            floats[fi++] = driftRelX;
            floats[fi++] = driftRelY;
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
            cumDriftX.Clear();
            cumDriftX.Add(0);
            cumDriftY.Clear();
            cumDriftY.Add(0);
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
                var vx = Math.Cos(windAngle) * windSpeed;
                var vy = Math.Sin(windAngle) * windSpeed + gravity;
                driftVxPerFrame.Add(vx);
                driftVyPerFrame.Add(vy);
                cumDriftX.Add(cumDriftX[ef] + vx / tableFps);
                cumDriftY.Add(cumDriftY[ef] + vy / tableFps);
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

        /// <summary>時刻teまでの風・重力の累積ドリフト積分(px)。フレーム内は線形補間</summary>
        (double X, double Y) GetDrift(double te)
        {
            if (te <= 0 || driftVxPerFrame.Count == 0)
                return (0, 0);
            var ef = Math.Clamp((int)Math.Floor(te * tableFps), 0, driftVxPerFrame.Count - 1);
            var frac = te - (double)ef / tableFps;
            return (cumDriftX[ef] + driftVxPerFrame[ef] * frac, cumDriftY[ef] + driftVyPerFrame[ef] * frac);
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

            var (driftBirthX, driftBirthY) = GetDrift(birth);

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
                (float)fade,
                driftBirthX,
                driftBirthY);

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
