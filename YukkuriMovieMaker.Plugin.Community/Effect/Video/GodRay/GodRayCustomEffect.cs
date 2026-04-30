using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GodRay
{
    internal class GodRayCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float LightX
        {
            set => SetValue((int)EffectImpl.Properties.LightX, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightX);
        }
        public float LightY
        {
            set => SetValue((int)EffectImpl.Properties.LightY, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightY);
        }
        public float Intensity
        {
            set => SetValue((int)EffectImpl.Properties.Intensity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Intensity);
        }
        public float Decay
        {
            set => SetValue((int)EffectImpl.Properties.Decay, value);
            get => GetFloatValue((int)EffectImpl.Properties.Decay);
        }
        public float Density
        {
            set => SetValue((int)EffectImpl.Properties.Density, value);
            get => GetFloatValue((int)EffectImpl.Properties.Density);
        }
        public float Weight
        {
            set => SetValue((int)EffectImpl.Properties.Weight, value);
            get => GetFloatValue((int)EffectImpl.Properties.Weight);
        }
        public float Samples
        {
            set => SetValue((int)EffectImpl.Properties.Samples, value);
            get => GetFloatValue((int)EffectImpl.Properties.Samples);
        }
        public float Threshold
        {
            set => SetValue((int)EffectImpl.Properties.Threshold, value);
            get => GetFloatValue((int)EffectImpl.Properties.Threshold);
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

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightX)]
            public float LightX
            {
                get => constants.LightX;
                set { constants.LightX = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightY)]
            public float LightY
            {
                get => constants.LightY;
                set { constants.LightY = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Intensity)]
            public float Intensity
            {
                get => constants.Intensity;
                set { constants.Intensity = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Decay)]
            public float Decay
            {
                get => constants.Decay;
                set { constants.Decay = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Density)]
            public float Density
            {
                get => constants.Density;
                set { constants.Density = Math.Max(value, 0f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Weight)]
            public float Weight
            {
                get => constants.Weight;
                set { constants.Weight = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Samples)]
            public float Samples
            {
                get => constants.Samples;
                set { constants.Samples = Math.Max(value, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Threshold)]
            public float Threshold
            {
                get => constants.Threshold;
                set { constants.Threshold = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorR)]
            public float ColorR
            {
                get => constants.ColorR;
                set { constants.ColorR = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorG)]
            public float ColorG
            {
                get => constants.ColorG;
                set { constants.ColorG = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorB)]
            public float ColorB
            {
                get => constants.ColorB;
                set { constants.ColorB = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorA)]
            public float ColorA
            {
                get => constants.ColorA;
                set { constants.ColorA = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("GodRay"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                var width = inputRect.Right - inputRect.Left;
                var height = inputRect.Bottom - inputRect.Top;

                var expandX = (int)(width * Math.Max(1f, constants.Density) * 2);
                var expandY = (int)(height * Math.Max(1f, constants.Density) * 2);

                outputRect = new RawRect(
                    inputRect.Left - expandX,
                    inputRect.Top - expandY,
                    inputRect.Right + expandX,
                    inputRect.Bottom + expandY
                );
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float LightX;
                public float LightY;
                public float Intensity;
                public float Decay;
                public float Density;
                public float Weight;
                public float Samples;
                public float Threshold;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
            }

            public enum Properties : int
            {
                LightX = 0,
                LightY = 1,
                Intensity = 2,
                Decay = 3,
                Density = 4,
                Weight = 5,
                Samples = 6,
                Threshold = 7,
                ColorR = 8,
                ColorG = 9,
                ColorB = 10,
                ColorA = 11,
            }
        }
    }
}
