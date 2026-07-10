using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AmbientOcclusion
{
    internal sealed class AmbientOcclusionCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Strength = 0,
            Radius,
            HeightGain,
            Directions,
            Samples,
            ShadowR,
            ShadowG,
            ShadowB,
            Suppression,
        }

        public float Strength { set => SetValue((int)PropertyIndex.Strength, value); }
        public float Radius { set => SetValue((int)PropertyIndex.Radius, value); }
        public float HeightGain { set => SetValue((int)PropertyIndex.HeightGain, value); }
        public float Directions { set => SetValue((int)PropertyIndex.Directions, value); }
        public float Samples { set => SetValue((int)PropertyIndex.Samples, value); }
        public float ShadowR { set => SetValue((int)PropertyIndex.ShadowR, value); }
        public float ShadowG { set => SetValue((int)PropertyIndex.ShadowG, value); }
        public float ShadowB { set => SetValue((int)PropertyIndex.ShadowB, value); }
        public float Suppression { set => SetValue((int)PropertyIndex.Suppression, value); }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Radius)]
            public float Radius { get => _cb.Radius; set { _cb.Radius = Math.Clamp(value, 1f, 256f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.HeightGain)]
            public float HeightGain { get => _cb.HeightGain; set { _cb.HeightGain = Math.Clamp(value, 0f, 8f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Directions)]
            public float Directions { get => _cb.Directions; set { _cb.Directions = Math.Clamp(value, 2f, 16f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Samples)]
            public float Samples { get => _cb.Samples; set { _cb.Samples = Math.Clamp(value, 1f, 12f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShadowR)]
            public float ShadowR { get => _cb.ShadowR; set { _cb.ShadowR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShadowG)]
            public float ShadowG { get => _cb.ShadowG; set { _cb.ShadowG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShadowB)]
            public float ShadowB { get => _cb.ShadowB; set { _cb.ShadowB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Suppression)]
            public float Suppression { get => _cb.Suppression; set { _cb.Suppression = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("AmbientOcclusion"))
            {
                _cb.Strength = 0.5f;
                _cb.Radius = 24f;
                _cb.HeightGain = 0.5f;
                _cb.Directions = 8f;
                _cb.Samples = 6f;
                _cb.ShadowR = 0.1f;
                _cb.ShadowG = 0.08f;
                _cb.ShadowB = 0.125f;
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            private int Padding()
            {
                return (int)Math.Ceiling(Math.Clamp(_cb.Radius, 1f, 256f)) + 2;
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 2)
                    throw new ArgumentException("InputRects must be length of 2", nameof(inputRects));

                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var padding = Padding();
                var padded = new RawRect(
                    Saturate((long)outputRect.Left - padding),
                    Saturate((long)outputRect.Top - padding),
                    Saturate((long)outputRect.Right + padding),
                    Saturate((long)outputRect.Bottom + padding));
                inputRects[0] = padded;
                inputRects[1] = padded;
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Strength;
                public float Radius;
                public float HeightGain;
                public float Directions;
                public float Samples;
                public float ShadowR;
                public float ShadowG;
                public float ShadowB;
                public float Suppression;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }
        }
    }
}
