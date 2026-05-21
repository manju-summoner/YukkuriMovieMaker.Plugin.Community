using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    internal sealed class EdgeGlowCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Threshold
        {
            set => SetValue((int)EffectImpl.Properties.Threshold, value);
            get => GetFloatValue((int)EffectImpl.Properties.Threshold);
        }
        public float Softness
        {
            set => SetValue((int)EffectImpl.Properties.Softness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Softness);
        }
        public float Thickness
        {
            set => SetValue((int)EffectImpl.Properties.Thickness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Thickness);
        }
        public float Intensity
        {
            set => SetValue((int)EffectImpl.Properties.Intensity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Intensity);
        }
        public float ColorR
        {
            set => SetValue((int)EffectImpl.Properties.ColorR, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorR);
        }
        public float ColorG
        {
            set => SetValue((int)EffectImpl.Properties.ColorG, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorG);
        }
        public float ColorB
        {
            set => SetValue((int)EffectImpl.Properties.ColorB, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorB);
        }
        public float ColorA
        {
            set => SetValue((int)EffectImpl.Properties.ColorA, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorA);
        }
        public int UseSourceColor
        {
            set => SetValue((int)EffectImpl.Properties.UseSourceColor, value);
            get => GetIntValue((int)EffectImpl.Properties.UseSourceColor);
        }
        public int IncludeAlpha
        {
            set => SetValue((int)EffectImpl.Properties.IncludeAlpha, value);
            get => GetIntValue((int)EffectImpl.Properties.IncludeAlpha);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Threshold)]
            public float Threshold { get => _cb.Threshold; set { _cb.Threshold = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Softness)]
            public float Softness { get => _cb.Softness; set { _cb.Softness = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Thickness)]
            public float Thickness { get => _cb.Thickness; set { _cb.Thickness = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Intensity)]
            public float Intensity { get => _cb.Intensity; set { _cb.Intensity = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorR)]
            public float ColorR { get => _cb.ColorR; set { _cb.ColorR = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorG)]
            public float ColorG { get => _cb.ColorG; set { _cb.ColorG = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorB)]
            public float ColorB { get => _cb.ColorB; set { _cb.ColorB = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorA)]
            public float ColorA { get => _cb.ColorA; set { _cb.ColorA = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.UseSourceColor)]
            public int UseSourceColor { get => _cb.UseSourceColor; set { _cb.UseSourceColor = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.IncludeAlpha)]
            public int IncludeAlpha { get => _cb.IncludeAlpha; set { _cb.IncludeAlpha = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("EdgeGlow"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
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

                int padding = ComputePadding();
                outputRect = new RawRect(
                    inputRect.Left - padding,
                    inputRect.Top - padding,
                    inputRect.Right + padding,
                    inputRect.Bottom + padding);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                int padding = ComputePadding();
                inputRects[0] = new RawRect(
                    outputRect.Left - padding,
                    outputRect.Top - padding,
                    outputRect.Right + padding,
                    outputRect.Bottom + padding);
            }

            private int ComputePadding()
            {
                int raw = (int)Math.Ceiling(_cb.Thickness) + 1;
                return Math.Min(raw, 4096);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Threshold;
                public float Softness;
                public float Thickness;
                public float Intensity;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
                public int UseSourceColor;
                public int IncludeAlpha;
                public int Pad0;
                public int Pad1;
            }

            public enum Properties : int
            {
                Threshold = 0,
                Softness = 1,
                Thickness = 2,
                Intensity = 3,
                ColorR = 4,
                ColorG = 5,
                ColorB = 6,
                ColorA = 7,
                UseSourceColor = 8,
                IncludeAlpha = 9,
            }
        }
    }
}
