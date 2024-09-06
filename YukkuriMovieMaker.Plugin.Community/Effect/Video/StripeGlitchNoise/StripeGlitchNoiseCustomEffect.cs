using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.StripeGlitchNoise
{
    public class StripeGlitchNoiseCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int Seed
        {
            get => GetIntValue((int)EffectImpl.Properties.Seed);
            set => SetValue((int)EffectImpl.Properties.Seed, value);
        }
        public int StripeCount
        {
            get => GetIntValue((int)EffectImpl.Properties.StripeCount);
            set => SetValue((int)EffectImpl.Properties.StripeCount, value);
        }
        public float InputTop
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputTop);
            set => SetValue((int)EffectImpl.Properties.InputTop, value);
        }
        public float InputHeight
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputHeight);
            set => SetValue((int)EffectImpl.Properties.InputHeight, value);
        }
        public float StripeMaxWidth
        {
            get => GetFloatValue((int)EffectImpl.Properties.StripeMaxWidth);
            set => SetValue((int)EffectImpl.Properties.StripeMaxWidth, value);
        }
        public float StripeMaxShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.StripeMaxShift);
            set => SetValue((int)EffectImpl.Properties.StripeMaxShift, value);
        }
        public float ColorMaxShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.ColorMaxShift);
            set => SetValue((int)EffectImpl.Properties.ColorMaxShift, value);
        }

        public bool IsHardBorder
        {
            get => GetBoolValue((int)EffectImpl.Properties.IsHardBorder);
            set => SetValue((int)EffectImpl.Properties.IsHardBorder, value);
        }

        public int Repeat
        {
            get => GetIntValue((int)EffectImpl.Properties.Repeat);
            set => SetValue((int)EffectImpl.Properties.Repeat, value);
        }

        public float StripeWidthAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.StripeWidthAttenuation);
            set => SetValue((int)EffectImpl.Properties.StripeWidthAttenuation, value);
        }

        public float StripeMaxShiftAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.StripeMaxShiftAttenuation);
            set => SetValue((int)EffectImpl.Properties.StripeMaxShiftAttenuation, value);
        }


        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Seed)]
            public int Seed
            {
                get => constants.Seed;
                set
                {
                    constants.Seed = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.StripeCount)]
            public int StripeCount
            {
                get => constants.StripeCount;
                set
                {
                    constants.StripeCount = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.InputTop)]
            public float InputTop
            {
                get => constants.InputTop;
                set
                {
                    constants.InputTop = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.InputHeight)]
            public float InputHeight
            {
                get => constants.InputHeight;
                set
                {
                    constants.InputHeight = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StripeMaxWidth)]
            public float StripeMaxWidth
            {
                get => constants.StripeMaxWidth;
                set
                {
                    constants.StripeMaxWidth = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StripeMaxShift)]
            public float StripeMaxShift
            {
                get => constants.StripeMaxShift;
                set
                {
                    constants.StripeMaxShift = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorMaxShift)]
            public float ColorMaxShift
            {
                get => constants.ColorMaxShift;
                set
                {
                    constants.ColorMaxShift = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsHardBorder)]
            public bool IsHardBorder { get; set; }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Repeat)]
            public int Repeat
            {
                get => constants.Repeat;
                set
                {
                    constants.Repeat = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StripeWidthAttenuation)]
            public float StripeWidthAttenuation
            {
                get => constants.StripeWidthAttenuation;
                set
                {
                    constants.StripeWidthAttenuation = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StripeMaxShiftAttenuation)]
            public float StripeMaxShiftAttenuation
            {
                get => constants.StripeMaxShiftAttenuation;
                set
                {
                    constants.StripeMaxShiftAttenuation = value;
                    UpdateConstants();
                }
            }


            public EffectImpl() : base(ShaderResourceUri.Get("StripeGlitchNoise"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var inputRect = inputRects[0];

                if (IsHardBorder)
                {
                    outputRect = inputRect;
                }
                else
                {
                    outputRect = new RawRect(
                        inputRect.Left - (int)StripeMaxShift - (int)ColorMaxShift,
                        inputRect.Top - (int)ColorMaxShift,
                        inputRect.Right + (int)StripeMaxShift + (int)ColorMaxShift,
                        inputRect.Bottom + (int)ColorMaxShift);
                }
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var stripeShift = (int)Math.Ceiling(StripeMaxShift);
                var colorShift = (int)Math.Ceiling(ColorMaxShift);

                inputRects[0] = new RawRect(
                    outputRect.Left - stripeShift - colorShift,
                    outputRect.Top - colorShift,
                    outputRect.Right + stripeShift + colorShift,
                    outputRect.Bottom + colorShift);
            }


            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public int Seed;
                public int StripeCount;
                public float InputTop;
                public float InputHeight;

                public float StripeMaxWidth;
                public float StripeMaxShift;
                public float ColorMaxShift;
                public int Repeat;

                public float StripeWidthAttenuation;
                public float StripeMaxShiftAttenuation;
            }
            public enum Properties : int
            {
                Seed = 0,
                StripeCount = 1,
                InputTop = 2,
                InputHeight = 3,

                StripeMaxWidth = 4,
                StripeMaxShift = 5,
                ColorMaxShift = 6,
                IsHardBorder = 7,

                Repeat = 8,
                StripeWidthAttenuation = 9,
                StripeMaxShiftAttenuation = 10,
            }
        }
    }
}
