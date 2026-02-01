using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AfterImage
{
    internal class AfterImageCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Strength
        {
            get => GetFloatValue((int)EffectImpl.Properties.Strength);
            set => SetValue((int)EffectImpl.Properties.Strength, value);
        }

        public int Mode
        {
            get => GetIntValue((int)EffectImpl.Properties.Mode);
            set => SetValue((int)EffectImpl.Properties.Mode, value);
        }

        [CustomEffect(2)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

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

            public override int GetInputCount() => 2;

            public EffectImpl() : base(ShaderResourceUri.Get("AfterImage"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                for(int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Strength;
                public int Mode;
            }
            public enum Properties : int
            {
                Strength = 0,
                Mode = 1,
            }
        }
    }
}