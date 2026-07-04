using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    // パス2: 構造テンソルをガウス平滑化してオリエンテーションを安定化。
    internal sealed class AnisotropicKuwaharaTensorBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public bool IsVertical
        {
            set => SetValue((int)EffectImpl.Properties.IsVertical, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsVertical);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            // シェーダー側 BLUR_RADIUS と一致させること
            const int ReadRadius = 5;

            private ConstantBuffer _cb = new() { DirX = 1f, DirY = 0f };

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsVertical)]
            public bool IsVertical
            {
                get => _cb.DirY != 0f;
                set { _cb.DirX = value ? 0f : 1f; _cb.DirY = value ? 1f : 0f; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("AnisotropicKuwaharaTensorBlur"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var padX = IsVertical ? 0 : ReadRadius + 1;
                var padY = IsVertical ? ReadRadius + 1 : 0;
                inputRects[0] = new RawRect(
                    outputRect.Left - padX,
                    outputRect.Top - padY,
                    outputRect.Right + padX,
                    outputRect.Bottom + padY);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float DirX;
                public float DirY;
            }

            public enum Properties : int
            {
                IsVertical = 0,
            }
        }
    }
}
