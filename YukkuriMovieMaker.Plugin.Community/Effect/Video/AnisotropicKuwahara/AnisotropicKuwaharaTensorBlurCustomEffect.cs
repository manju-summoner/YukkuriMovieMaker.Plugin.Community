using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    // パス2: 構造テンソルをガウス平滑化してオリエンテーションを安定化。パラメータ無し。
    internal sealed class AnisotropicKuwaharaTensorBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            // シェーダー側 BLUR_RADIUS と一致させること
            const int ReadRadius = 5;

            public EffectImpl() : base(ShaderResourceUri.Get("AnisotropicKuwaharaTensorBlur"))
            {
            }

            protected override void UpdateConstants()
            {
                // 定数バッファ無し
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = new RawRect(
                    outputRect.Left - ReadRadius - 1,
                    outputRect.Top - ReadRadius - 1,
                    outputRect.Right + ReadRadius + 1,
                    outputRect.Bottom + ReadRadius + 1);
            }
        }
    }
}
