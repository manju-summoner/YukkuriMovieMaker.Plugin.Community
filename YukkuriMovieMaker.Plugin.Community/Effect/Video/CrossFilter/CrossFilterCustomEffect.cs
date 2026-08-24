using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossFilter
{
    internal sealed class CrossFilterCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Strength = 0,
            Threshold,
            Length,
            RayCount,
            Angle,
            Dispersion,
            Thickness,
            LightOnly,
            LightR,
            LightG,
            LightB,
            Samples,
        }

        public float Strength { set => SetValue((int)PropertyIndex.Strength, value); }
        public float Threshold { set => SetValue((int)PropertyIndex.Threshold, value); }
        public float Length { set => SetValue((int)PropertyIndex.Length, value); }
        public float RayCount { set => SetValue((int)PropertyIndex.RayCount, value); }
        public float Angle { set => SetValue((int)PropertyIndex.Angle, value); }
        public float Dispersion { set => SetValue((int)PropertyIndex.Dispersion, value); }
        public float Thickness { set => SetValue((int)PropertyIndex.Thickness, value); }
        public int LightOnly { set => SetValue((int)PropertyIndex.LightOnly, value); }
        public float LightR { set => SetValue((int)PropertyIndex.LightR, value); }
        public float LightG { set => SetValue((int)PropertyIndex.LightG, value); }
        public float LightB { set => SetValue((int)PropertyIndex.LightB, value); }
        public float Samples { set => SetValue((int)PropertyIndex.Samples, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Clamp(value, 0f, 20f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Threshold)]
            public float Threshold { get => _cb.Threshold; set { _cb.Threshold = Math.Clamp(value, 0f, 0.999f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Length)]
            public float Length { get => _cb.Length; set { _cb.Length = Math.Clamp(value, 0f, 2000f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.RayCount)]
            public float RayCount { get => _cb.RayCount; set { _cb.RayCount = Math.Clamp(value, 1f, 16f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Angle)]
            public float Angle { get => _cb.Angle; set { _cb.Angle = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Dispersion)]
            public float Dispersion { get => _cb.Dispersion; set { _cb.Dispersion = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Thickness)]
            public float Thickness { get => _cb.Thickness; set { _cb.Thickness = Math.Clamp(value, 0f, 50f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.LightOnly)]
            public int LightOnly { get => _cb.LightOnly; set { _cb.LightOnly = value != 0 ? 1 : 0; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightR)]
            public float LightR { get => _cb.LightR; set { _cb.LightR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightG)]
            public float LightG { get => _cb.LightG; set { _cb.LightG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightB)]
            public float LightB { get => _cb.LightB; set { _cb.LightB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Samples)]
            public float Samples { get => _cb.Samples; set { _cb.Samples = Math.Clamp(value, 1f, 64f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("CrossFilter"))
            {
                _cb.Strength = 1f;
                _cb.Threshold = 0.6f;
                _cb.Length = 80f;
                _cb.RayCount = 4f;
                _cb.Samples = 24f;
                _cb.LightR = 1f;
                _cb.LightG = 1f;
                _cb.LightB = 1f;
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            private int Padding()
            {
                var margin = Math.Min(Math.Max(_cb.Length, 0f) + Math.Max(_cb.Thickness, 0f) + 2f, 4096f);
                return (int)Math.Ceiling(margin);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                var padding = Padding();
                outputRect = new RawRect(
                    inputRect.Left - padding,
                    inputRect.Top - padding,
                    inputRect.Right + padding,
                    inputRect.Bottom + padding);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    inputRects[0] = inputRect;
                    return;
                }

                var padding = Padding();
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - padding),
                    Saturate((long)outputRect.Top - padding),
                    Saturate((long)outputRect.Right + padding),
                    Saturate((long)outputRect.Bottom + padding));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Strength;
                public float Threshold;
                public float Length;
                public float RayCount;
                public float Angle;
                public float Dispersion;
                public float Thickness;
                public int LightOnly;
                public float LightR;
                public float LightG;
                public float LightB;
                public float Samples;
            }
        }
    }
}
