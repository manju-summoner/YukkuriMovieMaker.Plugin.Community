using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    // パス3: 本体。入力0=元画像(色)、入力1=平滑化済み構造テンソル。
    internal sealed class AnisotropicKuwaharaCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Radius
        {
            set => SetValue((int)EffectImpl.Properties.Radius, value);
            get => GetFloatValue((int)EffectImpl.Properties.Radius);
        }
        public float Sharpness
        {
            set => SetValue((int)EffectImpl.Properties.Sharpness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Sharpness);
        }
        public float Anisotropy
        {
            set => SetValue((int)EffectImpl.Properties.Anisotropy, value);
            get => GetFloatValue((int)EffectImpl.Properties.Anisotropy);
        }
        public int MaxN
        {
            set => SetValue((int)EffectImpl.Properties.MaxN, value);
            get => GetIntValue((int)EffectImpl.Properties.MaxN);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Radius)]
            public float Radius { get => _cb.RadiusPx; set { _cb.RadiusPx = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Sharpness)]
            public float Sharpness { get => _cb.Sharpness; set { _cb.Sharpness = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Anisotropy)]
            public float Anisotropy { get => _cb.Anisotropy; set { _cb.Anisotropy = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.MaxN)]
            public int MaxN { get => _cb.MaxN; set { _cb.MaxN = Math.Clamp(value, 1, 24); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("AnisotropicKuwahara"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                // 楕円の長軸は最大 2*radius まで伸びる
                int pad = Math.Min((int)Math.Ceiling(_cb.RadiusPx * 2f) + 2 + 1, MaxInputPixel);

                // 入力0(色)は近傍を読むので拡張
                inputRects[0] = new RawRect(
                    outputRect.Left - pad,
                    outputRect.Top - pad,
                    outputRect.Right + pad,
                    outputRect.Bottom + pad);

                // 入力1(テンソル)は中心のみ読むので出力領域と同じでよい
                inputRects[1] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float RadiusPx;
                public float Sharpness;
                public float Anisotropy;
                public int MaxN;
            }

            public enum Properties : int
            {
                Radius = 0,
                Sharpness = 1,
                Anisotropy = 2,
                MaxN = 3,
            }
        }
    }
}
