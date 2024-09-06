using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Binarization
{
    public class BinarizationCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Value
        {
            set => SetValue((int)EffectImpl.Properties.Value, value);
            get => GetFloatValue((int)EffectImpl.Properties.Value);
        }
        public bool IsInverted
        {
            set => SetValue((int)EffectImpl.Properties.IsInverted, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsInverted);
        }
        public bool KeepColor
        {
            set => SetValue((int)EffectImpl.Properties.KeepColor, value);
            get => GetBoolValue((int)EffectImpl.Properties.KeepColor);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Value)]
            public float Value
            {
                get
                {
                    return constants.Value;
                }
                set
                {
                    constants.Value = Math.Max(value, 0);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsInverted)]
            public bool IsInverted
            {
                get
                {
                    return constants.IsInverted;
                }
                set
                {
                    constants.IsInverted = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Bool, (int)Properties.KeepColor)]
            public bool KeepColor
            {
                get
                {
                    return constants.KeepColor;
                }
                set
                {
                    constants.KeepColor = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("Binarization"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }


            [StructLayout(LayoutKind.Explicit)]
            struct ConstantBuffer
            {
                [FieldOffset(0)]
                public float Value;
                [FieldOffset(4)]
                public bool IsInverted;
                [FieldOffset(8)]
                public bool KeepColor;
            }
            public enum Properties : int
            {
                Value = 0,
                IsInverted = 1,
                KeepColor = 2,
            }
        }
    }
}
