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
            const int Sectors = 8;
            const int MaxNLimit = 24;
            // サンプルは半径 n(≤MaxNLimit) の円内格子点。バッファ上限はその最大 N(MaxNLimit)。
            //   N(r) = Σ_{i=-r..r} ( 2*floor(sqrt(r*r - i*i)) + 1 )   … i²+j²≤r² の格子点数(ガウスの円問題)
            // 閉じた式が無く floor/sqrt を含むため const 式にできず直値。N(24)=1793。
            // MaxNLimit を変える場合は上式で N(MaxNLimit) を再計算して更新し、シェーダー側 MAX_SAMPLES とも一致させること。
            // 万一の不整合に備え、書き込みループは count < MaxSampleCount で保護している。
            const int MaxSampleCount = 1793;

            private ConstantBuffer _cb;
            private float _radius;
            private int _maxN = MaxNLimit;
            private int _tableN;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Radius)]
            public float Radius { get => _radius; set { _radius = Math.Max(value, 0f); _cb.RadiusPx = _radius; UpdateSampleTable(); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Sharpness)]
            public float Sharpness { get => _cb.Sharpness; set { _cb.Sharpness = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Anisotropy)]
            public float Anisotropy { get => _cb.Anisotropy; set { _cb.Anisotropy = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.MaxN)]
            public int MaxN { get => _maxN; set { _maxN = Math.Clamp(value, 1, MaxNLimit); UpdateSampleTable(); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("AnisotropicKuwahara"))
            {
                UpdateSampleTable();
            }

            private void UpdateSampleTable()
            {
                var n = Math.Clamp((int)Math.Round(_radius), 1, _maxN);
                if (n == _tableN)
                    return;
                _tableN = n;

                var count = 0;
                var sectorScale = Sectors / (2.0 * Math.PI);
                unsafe
                {
                    fixed (float* samples = _cb.Samples)
                    {
                        for (var j = -n; j <= n && count < MaxSampleCount; j++)
                        {
                            for (var i = -n; i <= n && count < MaxSampleCount; i++)
                            {
                                if (i * i + j * j > n * n)
                                    continue;

                                var ux = (double)i / n;
                                var uy = (double)j / n;
                                var r2 = ux * ux + uy * uy;
                                var wr = Math.Exp(-2.0 * r2);
                                var sf = (Math.Atan2(uy, ux) + Math.PI) * sectorScale;
                                if (sf >= Sectors)
                                    sf -= Sectors;

                                var p = samples + count * 4;
                                p[0] = (float)ux;
                                p[1] = (float)uy;
                                p[2] = (float)wr;
                                p[3] = (float)sf;
                                count++;
                            }
                        }
                    }
                }
                _cb.SampleCount = count;
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
            private unsafe struct ConstantBuffer
            {
                public float RadiusPx;
                public float Sharpness;
                public float Anisotropy;
                public int SampleCount;
                public fixed float Samples[MaxSampleCount * 4];
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
