using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RectangleGlitchNoise
{
    public class RectangleGlitchNoiseCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int Seed
        {
            get => GetIntValue((int)EffectImpl.Properties.Seed);
            set => SetValue((int)EffectImpl.Properties.Seed, value);
        }
        public int RectangleCount
        {
            get => GetIntValue((int)EffectImpl.Properties.RectangleCount);
            set => SetValue((int)EffectImpl.Properties.RectangleCount, value);
        }
        public float InputLeft
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputLeft);
            set => SetValue((int)EffectImpl.Properties.InputLeft, value);
        }
        public float InputTop
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputTop);
            set => SetValue((int)EffectImpl.Properties.InputTop, value);
        }
        public float InputWidth
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputWidth);
            set => SetValue((int)EffectImpl.Properties.InputWidth, value);
        }
        public float InputHeight
        {
            get => GetFloatValue((int)EffectImpl.Properties.InputHeight);
            set => SetValue((int)EffectImpl.Properties.InputHeight, value);
        }
        public float RectangleMaxWidth
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxWidth);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxWidth, value);
        }
        public float RectangleMaxHeight
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxHeight);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxHeight, value);
        }
        public float RectangleMaxXShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxXShift);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxXShift, value);
        }
        public float RectangleMaxYShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxYShift);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxYShift, value);
        }
        public float ColorMaxShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.ColorMaxShift);
            set => SetValue((int)EffectImpl.Properties.ColorMaxShift, value);
        }
        public bool IsClipping
        {
            get => GetBoolValue((int)EffectImpl.Properties.IsClipping);
            set => SetValue((int)EffectImpl.Properties.IsClipping, value);
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
        public float RectangleMaxWidthAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxWidthAttenuation);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxWidthAttenuation, value);
        }
        public float RectangleMaxHeightAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxHeightAttenuation);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxHeightAttenuation, value);
        }
        public float RectangleMaxXShiftAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxXShiftAttenuation);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxXShiftAttenuation, value);
        }
        public float RectangleMaxYShiftAttenuation
        {
            get => GetFloatValue((int)EffectImpl.Properties.RectangleMaxYShiftAttenuation);
            set => SetValue((int)EffectImpl.Properties.RectangleMaxYShiftAttenuation, value);
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
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.RectangleCount)]
            public int RectangleCount
            {
                get => constants.RectangleCount;
                set
                {
                    constants.RectangleCount = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.InputLeft)]
            public float InputLeft
            {
                get => constants.InputLeft;
                set
                {
                    constants.InputLeft = value;
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.InputWidth)]
            public float InputWidth
            {
                get => constants.InputWidth;
                set
                {
                    constants.InputWidth = value;
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxWidth)]
            public float RectangleMaxWidth
            {
                get => constants.RectangleMaxWidth;
                set
                {
                    constants.RectangleMaxWidth = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxHeight)]
            public float RectangleMaxHeight
            {
                get => constants.RectangleMaxHeight;
                set
                {
                    constants.RectangleMaxHeight = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxXShift)]
            public float RectangleMaxXShift
            {
                get => constants.RectangleMaxXShift;
                set
                {
                    constants.RectangleMaxXShift = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxYShift)]
            public float RectangleMaxYShift
            {
                get => constants.RectangleMaxYShift;
                set
                {
                    constants.RectangleMaxYShift = value;
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
            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsClipping)]
            public bool IsClipping
            {
                get => constants.IsClipping;
                set
                {
                    constants.IsClipping = value;
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxWidthAttenuation)]
            public float RectangleMaxWidthAttenuation
            {
                get => constants.RectangleMaxWidthAttenuation;
                set
                {
                    constants.RectangleMaxWidthAttenuation = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxHeightAttenuation)]
            public float RectangleMaxHeightAttenuation
            {
                get => constants.RectangleMaxHeightAttenuation;
                set
                {
                    constants.RectangleMaxHeightAttenuation = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxXShiftAttenuation)]
            public float RectangleMaxXShiftAttenuation
            {
                get => constants.RectangleMaxXShiftAttenuation;
                set
                {
                    constants.RectangleMaxXShiftAttenuation = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RectangleMaxYShiftAttenuation)]
            public float RectangleMaxYShiftAttenuation
            {
                get => constants.RectangleMaxYShiftAttenuation;
                set
                {
                    constants.RectangleMaxYShiftAttenuation = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("RectangleGlitchNoise"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var inputRect = inputRects[0];

                outputRect = IsHardBorder
                    ? inputRect
                    : new RawRect(
                        inputRect.Left - (int)RectangleMaxXShift - (int)ColorMaxShift,
                        inputRect.Top - (int)RectangleMaxYShift - (int)ColorMaxShift,
                        inputRect.Right + (int)RectangleMaxXShift + (int)ColorMaxShift,
                        inputRect.Bottom + (int)RectangleMaxYShift + (int)ColorMaxShift);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var rectangleXShift = (int)Math.Ceiling(RectangleMaxXShift);
                var rectangleYShift = (int)Math.Ceiling(RectangleMaxYShift);
                var colorShift = (int)Math.Ceiling(ColorMaxShift);

                inputRects[0] = new RawRect(
                    outputRect.Left - rectangleXShift - colorShift,
                    outputRect.Top - rectangleYShift - colorShift,
                    outputRect.Right + rectangleXShift + colorShift,
                    outputRect.Bottom + rectangleYShift + colorShift);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public int Seed;
                public int RectangleCount;
                public float InputLeft;
                public float InputTop;

                public float InputWidth;
                public float InputHeight;
                public float RectangleMaxWidth;
                public float RectangleMaxHeight;

                public float RectangleMaxXShift;
                public float RectangleMaxYShift;
                public float ColorMaxShift;
                public int Repeat;

                public float RectangleMaxWidthAttenuation;
                public float RectangleMaxHeightAttenuation;
                public float RectangleMaxXShiftAttenuation;
                public float RectangleMaxYShiftAttenuation;

                public bool IsClipping;
            }
            public enum Properties : int
            {
                Seed = 0,
                RectangleCount = 1,
                InputLeft = 2,
                InputTop = 3,

                InputWidth = 4,
                InputHeight = 5,
                RectangleMaxWidth = 6,
                RectangleMaxHeight = 7,

                RectangleMaxXShift = 8,
                RectangleMaxYShift = 9,
                ColorMaxShift = 10,
                IsClipping = 11,
                IsHardBorder = 12,

                Repeat = 13,
                RectangleMaxWidthAttenuation = 14,
                RectangleMaxHeightAttenuation = 15,
                RectangleMaxXShiftAttenuation = 16,

                RectangleMaxYShiftAttenuation = 17,
            }
        }
    }
}
