using System;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    sealed class FeatherAlphaCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        static readonly Uri shaderUri = new("pack://application:,,,/YukkuriMovieMaker;component/Resources/Shader/FeatherAlpha.cso");

        public float Smoothness
        {
            set => SetValue((int)EffectImpl.Properties.Smoothness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Smoothness);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            public EffectImpl() : base(shaderUri)
            {
                constants.Smoothness = 0.5f;
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Smoothness)]
            public float FeatherPx
            {
                get => constants.Smoothness;
                set
                {
                    constants.Smoothness = value;
                    UpdateConstants();
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));

                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = outputRect;
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Smoothness;
                public float Padding0;
                public float Padding1;
                public float Padding2;
            }

            public enum Properties : int
            {
                Smoothness = 0,
            }
        }
    }
}
