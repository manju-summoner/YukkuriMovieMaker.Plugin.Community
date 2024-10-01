using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorCorrection
{
    public class ColorCorrectionCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Lightness
        {
            set => SetValue((int)EffectImpl.Properties.Lightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Lightness);
        }
        public float Contrast
        {
            set => SetValue((int)EffectImpl.Properties.Contrast, value);
            get => GetFloatValue((int)EffectImpl.Properties.Contrast);
        }
        public float Hue
        {
            set => SetValue((int)EffectImpl.Properties.Hue, value);
            get => GetFloatValue((int)EffectImpl.Properties.Hue);
        }
        public float Brightness
        {
            set => SetValue((int)EffectImpl.Properties.Brightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Brightness);
        }
        public float Saturation
        {
            set => SetValue((int)EffectImpl.Properties.Saturation, value);
            get => GetFloatValue((int)EffectImpl.Properties.Saturation);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Lightness)]
            public float Lightness
            {
                get
                {
                    return constants.Lightness;
                }
                set
                {
                    constants.Lightness = Vortice.Mathematics.MathHelper.Clamp(value, 0f, 2f);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Contrast)]
            public float Contrast
            {
                get
                {
                    return constants.Contrast;
                }
                set
                {
                    constants.Contrast = Vortice.Mathematics.MathHelper.Clamp(value, 0f, 2f);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Hue)]
            public float Hue
            {
                get
                {
                    return constants.Hue;
                }
                set
                {
                    constants.Hue = Vortice.Mathematics.MathHelper.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Brightness)]
            public float Brightness
            {
                get
                {
                    return constants.Brightness;
                }
                set
                {
                    constants.Brightness = Vortice.Mathematics.MathHelper.Clamp(value, 0f, 2f);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Saturation)]
            public float Saturation
            {
                get
                {
                    return constants.Saturation;
                }
                set
                {
                    constants.Saturation = Vortice.Mathematics.MathHelper.Clamp(value, 0f, 2f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ColorCorrection"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Lightness;
                public float Contrast;

                public float Hue;
                public float Brightness;
                public float Saturation;
            }
            public enum Properties : int
            {
                Lightness = 0,
                Contrast = 1,
                Hue = 2,
                Brightness = 3,
                Saturation = 4,
            }
        }

    }
}
