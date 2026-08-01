using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    // パス1: 元画像から構造テンソルを計算(エンコードして出力)。パラメータ無し。
    internal sealed class AnisotropicKuwaharaTensorCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            // Sobel の読み取り半径
            const int ReadRadius = 1;

            public EffectImpl() : base(ShaderResourceUri.Get("AnisotropicKuwaharaTensor"))
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
