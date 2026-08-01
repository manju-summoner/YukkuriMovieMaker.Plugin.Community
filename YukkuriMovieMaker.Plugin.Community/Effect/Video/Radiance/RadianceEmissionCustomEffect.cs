using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceEmissionCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Threshold { set => SetValue((int)EffectImpl.Properties.Threshold, value); }
        public float Occlusion { set => SetValue((int)EffectImpl.Properties.Occlusion, value); }
        public float TintR { set => SetValue((int)EffectImpl.Properties.TintR, value); }
        public float TintG { set => SetValue((int)EffectImpl.Properties.TintG, value); }
        public float TintB { set => SetValue((int)EffectImpl.Properties.TintB, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb = new() { Threshold = 0.7f, Occlusion = 0.8f, TintR = 1f, TintG = 1f, TintB = 1f };

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Threshold)]
            public float Threshold { get => _cb.Threshold; set { _cb.Threshold = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Occlusion)]
            public float Occlusion { get => _cb.Occlusion; set { _cb.Occlusion = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TintR)]
            public float TintR { get => _cb.TintR; set { _cb.TintR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TintG)]
            public float TintG { get => _cb.TintG; set { _cb.TintG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TintB)]
            public float TintB { get => _cb.TintB; set { _cb.TintB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceEmission"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = new RawRect(
                    outputRect.Left - 1,
                    outputRect.Top - 1,
                    outputRect.Right + 1,
                    outputRect.Bottom + 1);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Threshold;
                public float Occlusion;
                public float Pad0;
                public float Pad1;
                public float TintR;
                public float TintG;
                public float TintB;
                public float Pad2;
            }

            public enum Properties : int
            {
                Threshold = 0,
                Occlusion = 1,
                TintR = 2,
                TintG = 3,
                TintB = 4,
            }
        }
    }
}
