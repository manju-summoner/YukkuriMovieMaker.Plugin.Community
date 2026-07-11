using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    /// <summary>
    /// パーティクル出力エフェクトの描画プロセッサ。
    /// エフェクトチェーンは inputCache（Cached=true）→ ParticleOutputCustomEffect →（元画像表示時はComposite）→ terminal。
    /// 頂点バッファ（スロット数）は静的なため、発生頻度×寿命から決まるスロット数が変わったらエフェクトごと作り直す。
    /// 生成したD2DリソースはDisposeCollectorに登録する（寿命はプロセッサと同じ）。
    /// </summary>
    internal sealed class ParticleOutputEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly ParticleOutputEffect item;

        public ParticleOutputEffectProcessor(IGraphicsDevicesAndContext devices, ParticleOutputEffect item) : base(devices)
        {
            //基底コンストラクタがCreateEffectを呼ぶため、フィールドへの保存はその後になる
            //（CreateEffect内では引数のdevicesを使う）
            this.devices = devices;
            this.item = item;
        }

        AffineTransform2D inputCache = null!;
        ParticleOutputCustomEffect? particle;
        ID2D1Image? particleOutput;
        Composite composite = null!;
        ID2D1Image? compositeOutput;
        AffineTransform2D terminal = null!;

        bool isFirst = true;
        InterpolationMode interpolationMode;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || input is null)
                return effectDescription.DrawDescription;

            var dc = devices.DeviceContext;
            //上流に無限大のローカル境界を返すエフェクト（単色塗りつぶし等の生成系）があると
            //頂点座標や描画範囲が非有限になり描画が破綻するため、有限範囲へクランプする
            var bounds = ClampBounds(dc.GetImageLocalBounds(input));

            var interpolationMode = effectDescription.DrawDescription.ZoomInterpolationMode;
            if (isFirst || this.interpolationMode != interpolationMode)
            {
                inputCache.InterPolationMode = interpolationMode.ToTransform2D();
                terminal.InterPolationMode = interpolationMode.ToTransform2D();
                if (particle is not null)
                    particle.NearestNeighbor = interpolationMode is InterpolationMode.NearestNeighbor;
            }
            isFirst = false;
            this.interpolationMode = interpolationMode;

            //スロット数 = 同時に生存しうる粒子数（発生頻度×寿命）。周期がちょうど寿命だと
            //再利用時に前の粒子と入れ替わる瞬間が見えるため、+1して周期>寿命を保証する
            var rate = Math.Clamp(item.Rate, 0.1, 2000);
            var lifetime = Math.Clamp(item.Lifetime, 0.01, 20);
            var slotCount = Math.Clamp((int)Math.Ceiling(rate * lifetime) + 1, 1, ParticleOutputCustomEffect.MaxParticles);
            EnsureCapacity(slotCount);
            if (particle is null)
            {
                //カスタムエフェクトを生成できない環境ではパススルー
                terminal.SetInput(0, input, true);
                return effectDescription.DrawDescription;
            }

            var time = effectDescription.ItemPosition.Time.TotalSeconds + Math.Max(0, item.Preroll);

            var boundsCenter = new Vector2((bounds.Left + bounds.Right) * 0.5f, (bounds.Top + bounds.Bottom) * 0.5f);
            var boundsHalf = new Vector2(
                MathF.Max(0, (bounds.Right - bounds.Left) * 0.5f),
                MathF.Max(0, (bounds.Bottom - bounds.Top) * 0.5f));
            var patchHalf = boundsHalf * (float)(Math.Max(0.1, item.Size) / 100);
            //発生範囲は中心からの最大距離(px)。縦横同じ幅の矩形分布
            var emitRange = new Vector2((float)Math.Max(0, item.EmitRange));

            var emitAngle = (float)(item.EmitAngle * Math.PI / 180);
            var spreadHalf = (float)(Math.Clamp(item.SpreadAngle, 0, 360) * Math.PI / 180 / 2);
            var windAngle = (float)(item.WindAngle * Math.PI / 180);
            var speed = (float)Math.Max(0, item.Speed);
            var gravity = (float)item.Gravity;
            var windSpeed = (float)Math.Max(0, item.WindSpeed);
            var endScale = (float)(Math.Max(0, item.EndScale) / 100);

            var constants = new ParticleOutputCustomEffect.ConstantBuffer
            {
                Time = (float)time,
                EmitInterval = (float)(1 / rate),
                LifetimeInv = (float)(1 / lifetime),
                Randomness = Math.Clamp((float)(item.Randomness / 100), 0, 1),
                BoundsCenter = boundsCenter,
                BoundsHalf = boundsHalf,
                EmitAngle = emitAngle,
                SpreadHalfAngle = spreadHalf,
                Speed = speed,
                Gravity = gravity,
                Turbulence = (float)(Math.Max(0, item.Turbulence) / 100 * 3),//渦の角速度(rad/s)、100%=3rad/s
                RotationSpeed = (float)(Math.Max(0, item.Rotation) * Math.PI / 180),
                EndScale = endScale,
                Fade = Math.Clamp((float)(item.Fade / 100), 0, 1),
                Seed = (item.GetHashCode() & 0xFFFF) / 65536f,
                Wind = new Vector2(MathF.Cos(windAngle), MathF.Sin(windAngle)) * windSpeed,
                SlotCount = particle.SlotCount,
                PatchHalf = patchHalf,
                EmitRange = emitRange,
            };

            //出力範囲：入力範囲を粒子の最大変位ぶん広げる。
            //初速項の変位はease-outで v0×寿命/1.5 に飽和し、風・重力項は経過時間（寿命でクランプ）に比例する。
            //粒子はばらつきで最大1.5倍まで大きくなるため、コーナー分は1.5倍×サイズ変化で見積もる
            if (time <= 0)
            {
                //まだ粒子が1つも存在しない。極小の出力範囲で空描画にする
                particle.DeformedLeft = -1;
                particle.DeformedTop = -1;
                particle.DeformedRight = 1;
                particle.DeformedBottom = 1;
            }
            else
            {
                var tauMax = (float)Math.Min(time, lifetime);
                var cornerRadius = patchHalf.Length() * 1.5f * MathF.Max(1, endScale);
                var expand =
                    speed * (float)lifetime / 1.5f
                    + (windSpeed + MathF.Abs(gravity)) * tauMax
                    + MathF.Max(emitRange.X, emitRange.Y)
                    + cornerRadius;
                particle.DeformedLeft = bounds.Left - expand;
                particle.DeformedTop = bounds.Top - expand;
                particle.DeformedRight = bounds.Right + expand;
                particle.DeformedBottom = bounds.Bottom + expand;
            }
            particle.SetConstants(constants);

            terminal.SetInput(0, item.ShowOriginal ? compositeOutput : particleOutput, true);
            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            //環境がカスタムエフェクトに対応しているかを最小の頂点データで確認する
            using (var probe = new ParticleOutputCustomEffect(devices, new byte[6 * ParticleOutputCustomEffect.VertexStride]))
            {
                if (!probe.IsEnabled)
                    return null;
            }

            //粒子描画のたびに上流を再評価しないよう、入力をキャッシュしておく
            inputCache = new AffineTransform2D(devices.DeviceContext) { Cached = true };
            disposer.Collect(inputCache);

            //「元の画像を表示」用の合成ノード。粒子（下）の上に元画像（上）を重ねる
            composite = new Composite(devices.DeviceContext) { InputCount = 2 };
            disposer.Collect(composite);
            compositeOutput = composite.Output;
            disposer.Collect(compositeOutput);

            //出力ノード。元画像表示の有無に応じてComposite/粒子出力を切り替える
            terminal = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(terminal);

            var result = terminal.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void setInput(ID2D1Image? input)
        {
            inputCache?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            inputCache?.SetInput(0, null, true);
            particle?.SetInput(0, null, true);
            composite?.SetInput(0, null, true);
            composite?.SetInput(1, null, true);
            terminal?.SetInput(0, null, true);
        }

        //入力境界のクランプ範囲。D2Dの最大ビットマップサイズ（通常16384）より十分大きく、float演算が壊れない値
        const float MaxBoundsExtent = 1 << 22;

        static Vortice.RawRectF ClampBounds(Vortice.RawRectF bounds)
        {
            return new Vortice.RawRectF(
                ClampCoordinate(bounds.Left),
                ClampCoordinate(bounds.Top),
                ClampCoordinate(bounds.Right),
                ClampCoordinate(bounds.Bottom));

            static float ClampCoordinate(float value)
                => float.IsNaN(value) ? 0f : Math.Clamp(value, -MaxBoundsExtent, MaxBoundsExtent);
        }

        /// <summary>
        /// スロット数に合った静的頂点バッファを持つカスタムエフェクトを用意する。
        /// 頂点バッファはInitializeでしか生成できないため、スロット数が変わったらエフェクトごと作り直す。
        /// </summary>
        void EnsureCapacity(int slotCount)
        {
            if (particle is not null && particle.SlotCount == slotCount)
                return;

            var vertexData = ParticleOutputVertexBufferBuilder.Build(slotCount);
            var newEffect = new ParticleOutputCustomEffect(devices, vertexData);
            if (!newEffect.IsEnabled)
            {
                //作り直しに失敗した場合は現在のエフェクトのまま描画を続ける
                newEffect.Dispose();
                return;
            }

            if (particle is not null)
            {
                particle.SetInput(0, null, true);
                disposer.RemoveAndDispose(ref particleOutput);
                disposer.RemoveAndDispose(ref particle);
            }

            particle = newEffect;
            disposer.Collect(particle);
            using (var output = inputCache.Output)
                particle.SetInput(0, output, true);
            particleOutput = particle.Output;
            disposer.Collect(particleOutput);

            particle.NearestNeighbor = interpolationMode is InterpolationMode.NearestNeighbor;

            //合成ノードへ配線し直す（下:粒子、上:元画像）
            composite.SetInput(0, particleOutput, true);
            using (var cached = inputCache.Output)
                composite.SetInput(1, cached, true);
        }
    }
}
