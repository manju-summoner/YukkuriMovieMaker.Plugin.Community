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
    /// エフェクトチェーンは inputCache（Cached=true）→ ParticleOutputCustomEffect → terminal。
    /// 毎フレーム <see cref="ParticleOutputParticleBuilder"/> が生存粒子の頂点データを構築し、ポインタ渡しでGPUへ転送する。
    /// 頂点バッファの容量が足りなくなったらカスタムエフェクトごと作り直す（CreateVertexBufferはInitialize中しか呼べないため）。
    /// 生成したD2DリソースはDisposeCollectorに登録する（寿命はプロセッサと同じ）。
    /// </summary>
    internal sealed class ParticleOutputEffectProcessor : VideoEffectProcessorBase
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly ParticleOutputEffect item;
        readonly ParticleOutputParticleBuilder builder = new();

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

            var fps = effectDescription.FPS;
            var lengthFrames = effectDescription.ItemDuration.Frame;
            var preroll = Math.Clamp(item.Preroll, 0, 100);
            var prerollFrames = (int)Math.Round(preroll * fps);
            var te = effectDescription.ItemPosition.Time.TotalSeconds + preroll;

            var result = builder.Build(item, te, fps, lengthFrames, prerollFrames, item.GetHashCode(), bounds);

            EnsureCapacity(result.ByteCount);
            if (particle is null)
            {
                //カスタムエフェクトを生成できない環境ではパススルー
                terminal.SetInput(0, input, true);
                return effectDescription.DrawDescription;
            }

            particle.SetVertexData(result.Pointer, result.ByteCount);
            particle.VertexCount = result.VertexCount;
            particle.SetConstants(new ParticleOutputCustomEffect.ConstantBuffer
            {
                BoundsCenter = new Vector2((bounds.Left + bounds.Right) * 0.5f, (bounds.Top + bounds.Bottom) * 0.5f),
                BoundsHalf = new Vector2(
                    MathF.Max(0, (bounds.Right - bounds.Left) * 0.5f),
                    MathF.Max(0, (bounds.Bottom - bounds.Top) * 0.5f)),
                Perspective = new Vector4(
                    result.PerspectiveFocalLength,
                    result.PerspectiveNearDenominator,
                    result.PerspectiveFocalLength > 0 ? 1 : 0,
                    0),
            });

            if (result.HasParticles)
            {
                particle.DeformedLeft = result.Left;
                particle.DeformedTop = result.Top;
                particle.DeformedRight = result.Right;
                particle.DeformedBottom = result.Bottom;
            }
            else
            {
                //粒子が1つも無い。極小の出力範囲で空描画にする
                particle.DeformedLeft = -1;
                particle.DeformedTop = -1;
                particle.DeformedRight = 1;
                particle.DeformedBottom = 1;
            }

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            //環境がカスタムエフェクトに対応しているかを確認しつつ、初期容量のエフェクトを作る
            var probe = new ParticleOutputCustomEffect(devices, ParticleOutputCustomEffect.InitialVertexBufferByteSize);
            if (!probe.IsEnabled)
            {
                probe.Dispose();
                return null;
            }
            particle = probe;
            disposer.Collect(particle);

            //粒子描画のたびに上流を再評価しないよう、入力をキャッシュしておく
            inputCache = new AffineTransform2D(devices.DeviceContext) { Cached = true };
            disposer.Collect(inputCache);

            using (var output = inputCache.Output)
                particle.SetInput(0, output, true);
            particleOutput = particle.Output;
            disposer.Collect(particleOutput);

            //出力ノード。常に粒子出力を接続する
            terminal = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(terminal);
            terminal.SetInput(0, particleOutput, true);

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
        /// 頂点データが収まる容量のカスタムエフェクトを用意する。
        /// 頂点バッファはInitializeでしか生成できないため、容量が足りなくなったらエフェクトごと作り直す。
        /// </summary>
        void EnsureCapacity(int byteCount)
        {
            if (particle is null || particle.VertexBufferByteSize >= byteCount)
                return;

            var newSize = particle.VertexBufferByteSize;
            while (newSize < byteCount)
                newSize *= 2;
            newSize = Math.Min(newSize, ParticleOutputCustomEffect.MaxVertices * ParticleOutputCustomEffect.VertexStride);

            var newEffect = new ParticleOutputCustomEffect(devices, newSize);
            if (!newEffect.IsEnabled)
            {
                //作り直しに失敗した場合は現在のエフェクトのまま描画を続ける（描画範囲はクランプされる）
                newEffect.Dispose();
                return;
            }

            particle.SetInput(0, null, true);
            disposer.RemoveAndDispose(ref particleOutput);
            disposer.RemoveAndDispose(ref particle);

            particle = newEffect;
            disposer.Collect(particle);
            using (var output = inputCache.Output)
                particle.SetInput(0, output, true);
            particleOutput = particle.Output;
            disposer.Collect(particleOutput);

            particle.NearestNeighbor = interpolationMode is InterpolationMode.NearestNeighbor;

            terminal.SetInput(0, particleOutput, true);
        }
    }
}
