using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    /// <summary>
    /// 粒子化系エフェクト（粒子化・粒子化しながら登場退場）の共通描画コンポーネント。
    /// エフェクトチェーンは inputCache（Cached=true）→ ParticlizeCustomEffect → terminal で、
    /// 粒子化前は terminal の入力を直結（パススルー）に切り替える。
    /// 頂点バッファは静的なため、入力範囲・粒子サイズが変わったらエフェクトごと作り直す。
    /// 生成したD2Dリソースはコンストラクタで受け取ったDisposeCollectorに登録する（寿命はプロセッサと同じ）。
    /// </summary>
    internal sealed class ParticlizeRenderer(IGraphicsDevicesAndContext devices, DisposeCollector disposer)
    {
        /// <summary>粒子運動のパラメータ（各%値は0-100基準）</summary>
        public readonly record struct Parameter(
            double Size,
            double DissolveSpanSeconds,
            double AngleDegree,
            double Randomness,
            double LifetimeSeconds,
            double ScatterAngleDegree,
            double Speed,
            double Spread,
            double WindAngleDegree,
            double WindSpeed,
            double Gravity,
            double Turbulence,
            double Rotation,
            double Shrink,
            double Fade,
            int Seed);

        AffineTransform2D inputCache = null!;
        ParticlizeCustomEffect? particlize;
        ID2D1Image? particlizeOutput;
        AffineTransform2D terminal = null!;

        //頂点バッファ（グリッド）のキャッシュキー
        bool hasGrid;
        float gridLeft, gridTop, gridRight, gridBottom;
        double gridSize;

        bool isFirst = true;
        InterpolationMode interpolationMode;

        /// <summary>
        /// エフェクトチェーンを構築して出力ノードを返す。カスタムエフェクト非対応環境ではnull。
        /// </summary>
        public ID2D1Image? CreateEffect()
        {
            //環境がカスタムエフェクトに対応しているかを最小の頂点データで確認する
            using (var probe = new ParticlizeCustomEffect(devices, new byte[6 * ParticlizeCustomEffect.VertexStride]))
            {
                if (!probe.IsEnabled)
                    return null;
            }

            //粒子化している間は粒子描画のたびに上流を再評価しないよう、入力をキャッシュしておく
            inputCache = new AffineTransform2D(devices.DeviceContext) { Cached = true };
            disposer.Collect(inputCache);

            //出力ノード。粒子化前は入力を直結（パススルー）、粒子化中はParticlizeCustomEffectへ切り替える
            terminal = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(terminal);

            var result = terminal.Output;
            disposer.Collect(result);
            return result;
        }

        public void SetInput(ID2D1Image? input)
        {
            inputCache?.SetInput(0, input, true);
        }

        public void ClearEffectChain()
        {
            inputCache?.SetInput(0, null, true);
            particlize?.SetInput(0, null, true);
            terminal?.SetInput(0, null, true);
        }

        /// <summary>
        /// 描画状態を更新する。timeは粒子化開始からの経過時間（0以下でパススルー）。
        /// </summary>
        public void Update(EffectDescription effectDescription, ID2D1Image? input, TimeSpan time, in Parameter parameter)
        {
            var dc = devices.DeviceContext;
            //上流に無限大のローカル境界を返すエフェクト（単色塗りつぶし等の生成系）があると
            //頂点座標や描画範囲が非有限になり描画が破綻するため、有限範囲へクランプする
            var bounds = ClampBounds(dc.GetImageLocalBounds(input));

            var interpolationMode = effectDescription.DrawDescription.ZoomInterpolationMode;
            if (isFirst || this.interpolationMode != interpolationMode)
            {
                inputCache.InterPolationMode = interpolationMode.ToTransform2D();
                terminal.InterPolationMode = interpolationMode.ToTransform2D();
                if (particlize is not null)
                    particlize.NearestNeighbor = interpolationMode is InterpolationMode.NearestNeighbor;
            }
            isFirst = false;
            this.interpolationMode = interpolationMode;

            if (time <= TimeSpan.Zero)
            {
                terminal.SetInput(0, input, true);
                return;
            }

            EnsureParticleGrid(bounds, Math.Max(1, parameter.Size));
            if (particlize is null)
            {
                //カスタムエフェクトを生成できない環境ではパススルー
                terminal.SetInput(0, input, true);
                return;
            }

            //消える方向に沿った進行度の正規化範囲（入力範囲の4隅の射影の最小・最大）
            var angle = (float)(parameter.AngleDegree * Math.PI / 180);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Span<float> orders =
            [
                Vector2.Dot(new Vector2(bounds.Left, bounds.Top), direction),
                Vector2.Dot(new Vector2(bounds.Right, bounds.Top), direction),
                Vector2.Dot(new Vector2(bounds.Left, bounds.Bottom), direction),
                Vector2.Dot(new Vector2(bounds.Right, bounds.Bottom), direction),
            ];
            var orderMin = Math.Min(Math.Min(orders[0], orders[1]), Math.Min(orders[2], orders[3]));
            var orderMax = Math.Max(Math.Max(orders[0], orders[1]), Math.Max(orders[2], orders[3]));
            var orderRange = orderMax - orderMin;

            var scatterAngle = (float)(parameter.ScatterAngleDegree * Math.PI / 180);
            var windAngle = (float)(parameter.WindAngleDegree * Math.PI / 180);
            //%パラメータの実値へのマッピング（100%基準）
            var speed = (float)(parameter.Speed * 2);            //初速(px/s)
            var spread = (float)(parameter.Spread * 2);          //初速(px/s)
            var windSpeed = (float)(parameter.WindSpeed * 2);    //終端速度(px/s)
            var gravity = (float)(parameter.Gravity * 2);        //終端速度(px/s)
            var turbulence = (float)(parameter.Turbulence / 100 * 3);//渦の角速度(rad/s)
            var rotationSpeed = (float)(parameter.Rotation / 100 * 2 * Math.PI);//rad/s
            var lifetime = Math.Max(0.01, parameter.LifetimeSeconds);

            var constants = new ParticlizeCustomEffect.ConstantBuffer
            {
                Time = (float)time.TotalSeconds,
                DissolveSpan = (float)Math.Max(0, parameter.DissolveSpanSeconds),
                LifetimeInv = (float)(1 / lifetime),
                Randomness = Math.Clamp((float)(parameter.Randomness / 100), 0, 1),
                DissolveDirection = direction,
                OrderMin = orderMin,
                OrderInvRange = orderRange > 1e-3f ? 1 / orderRange : 0,
                ScatterVelocity = new Vector2(MathF.Cos(scatterAngle), MathF.Sin(scatterAngle)) * speed,
                SpreadVelocity = spread,
                Gravity = gravity,
                Turbulence = turbulence,
                RotationSpeed = rotationSpeed,
                Shrink = Math.Clamp((float)(parameter.Shrink / 100), 0, 1),
                Fade = Math.Clamp((float)(parameter.Fade / 100), 0, 1),
                Seed = (parameter.Seed & 0xFFFF) / 65536f,
                Wind = new Vector2(MathF.Cos(windAngle), MathF.Sin(windAngle)) * windSpeed,
            };

            //出力範囲：粒子の最大変位ぶん入力範囲を広げる。
            //粒子毎の経過時間は寿命でクランプされ、さらに移動時間はease-outで飽和する
            //（寿命が尽きた粒子は描画されない）ため、変位の上限は寿命までの移動量で抑えられる。
            //粒子は最大1.5倍まで膨張するためコーナー分も1.5倍で見積もる
            var totalSeconds = time.TotalSeconds;
            var tauMax = Math.Min(totalSeconds, lifetime);
            var maxCorner = (MathF.Max(gridRight - gridLeft, gridBottom - gridTop) / ParticlizeCustomEffect.MaxCellsPerAxis + (float)parameter.Size) * 1.5f;
            var expand = (float)((speed + spread + windSpeed + Math.Abs(gravity)) * tauMax + maxCorner);
            var fullyDissolved = totalSeconds >= constants.DissolveSpan + lifetime;
            if (fullyDissolved)
            {
                //すべての粒子が寿命を迎えて非表示。極小の出力範囲で空描画にする
                particlize.DeformedLeft = -1;
                particlize.DeformedTop = -1;
                particlize.DeformedRight = 1;
                particlize.DeformedBottom = 1;
            }
            else
            {
                particlize.DeformedLeft = bounds.Left - expand;
                particlize.DeformedTop = bounds.Top - expand;
                particlize.DeformedRight = bounds.Right + expand;
                particlize.DeformedBottom = bounds.Bottom + expand;
            }
            particlize.SetConstants(constants);

            terminal.SetInput(0, particlizeOutput, true);
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
        /// 入力範囲・粒子サイズに合った静的頂点バッファを持つカスタムエフェクトを用意する。
        /// 頂点バッファはInitializeでしか生成できないため、グリッドが変わったらエフェクトごと作り直す。
        /// </summary>
        void EnsureParticleGrid(Vortice.RawRectF bounds, double size)
        {
            if (hasGrid
                && particlize is not null
                && GridBoundsNearlyEqual(new(gridLeft, gridTop, gridRight, gridBottom), bounds)
                && gridSize == size)
                return;

            var vertexData = ParticlizeVertexBufferBuilder.Build(bounds, size);
            var newEffect = new ParticlizeCustomEffect(devices, vertexData);
            if (!newEffect.IsEnabled)
            {
                //作り直しに失敗した場合は現在のエフェクトのまま描画を続ける
                newEffect.Dispose();
                return;
            }

            if (particlize is not null)
            {
                particlize.SetInput(0, null, true);
                disposer.RemoveAndDispose(ref particlizeOutput);
                disposer.RemoveAndDispose(ref particlize);
            }

            particlize = newEffect;
            disposer.Collect(particlize);
            using (var output = inputCache.Output)
                particlize.SetInput(0, output, true);
            particlizeOutput = particlize.Output;
            disposer.Collect(particlizeOutput);

            particlize.NearestNeighbor = interpolationMode is InterpolationMode.NearestNeighbor;

            hasGrid = true;
            gridLeft = bounds.Left;
            gridTop = bounds.Top;
            gridRight = bounds.Right;
            gridBottom = bounds.Bottom;
            gridSize = size;
        }

        internal static bool GridBoundsNearlyEqual(Vortice.RawRectF cached, Vortice.RawRectF current)
            => MathF.Abs(cached.Left - current.Left) < 0.5f
                && MathF.Abs(cached.Top - current.Top) < 0.5f
                && MathF.Abs(cached.Right - current.Right) < 0.5f
                && MathF.Abs(cached.Bottom - current.Bottom) < 0.5f;
    }
}
