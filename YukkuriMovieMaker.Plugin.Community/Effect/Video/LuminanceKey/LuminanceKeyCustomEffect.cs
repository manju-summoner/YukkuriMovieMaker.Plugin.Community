using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuminanceKey
{
    internal class LuminanceKeyCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Threshold
        {
            get=>GetFloatValue((int)EffectImpl.Properties.Threshold);
            set=>SetValue((int)EffectImpl.Properties.Threshold, value);
        }
        public float Smoothness
        {
            get=>GetFloatValue((int)EffectImpl.Properties.Smoothness);
            set=>SetValue((int)EffectImpl.Properties.Smoothness, value);
        }
        public int Mode
        { 
            get=>GetIntValue((int)EffectImpl.Properties.Mode);
            set=>SetValue((int)EffectImpl.Properties.Mode, value);
        }
        public int IsInvert
        {
            get=>GetIntValue((int)EffectImpl.Properties.IsInvert);
            set=>SetValue((int)EffectImpl.Properties.IsInvert, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Threshold)]
            public float Threshold
            {
                get
                {
                    return constants.Threshold;
                }
                set
                {
                    constants.Threshold = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Smoothness)]
            public float Smoothness
            {
                get
                {
                    return constants.Smoothness;
                }
                set
                {
                    constants.Smoothness = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Mode)]
            public int Mode
            {
                get
                {
                    return constants.Mode;
                }
                set
                {
                    constants.Mode = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.IsInvert)]
            public int IsInvert
            {
                get
                {
                    return constants.IsInvert;
                }
                set
                {
                    constants.IsInvert = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("LuminanceKey"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Threshold;
                public float Smoothness;
                public int Mode;
                public int IsInvert;
            }
            public enum Properties : int
            {
                Threshold,
                Smoothness,
                Mode,
                IsInvert,
            }
        }
    }
}