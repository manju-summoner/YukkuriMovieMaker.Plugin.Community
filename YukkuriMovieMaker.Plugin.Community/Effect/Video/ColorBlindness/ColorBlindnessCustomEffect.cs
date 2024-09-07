using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorBlindness
{
    public class ColorBlindnessCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int Type
        {
            set => SetValue((int)EffectImpl.Properties.Type, value);
            get => GetIntValue((int)EffectImpl.Properties.Type);
        }
        public float Strength
        {
            set => SetValue((int)EffectImpl.Properties.Strength, value);
            get => GetFloatValue((int)EffectImpl.Properties.Strength);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Type)]
            public int Type
            {
                get
                {
                    return constants.Type;
                }
                set
                {
                    constants.Type = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Strength)]
            public float Strength
            {
                get
                {
                    return constants.Strength;
                }
                set
                {
                    constants.Strength = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ColorBlindness"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public int Type;
                public float Strength;
            }
            public enum Properties : int
            {
                Type = 0,
                Strength = 1,
            }
        }

    }
}
