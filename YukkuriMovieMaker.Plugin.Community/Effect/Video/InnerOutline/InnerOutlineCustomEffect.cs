using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    public class InnerOutlineCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Thickness
        {
            set => SetValue((int)EffectImpl.Properties.Thickness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Thickness);
        }

        public float Margin
        {
            set => SetValue((int)EffectImpl.Properties.Margin, value);
            get => GetFloatValue((int)EffectImpl.Properties.Margin);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer constants;

            public EffectImpl() : base(ShaderResourceUri.Get("InnerOutline"))
            {
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Thickness)]
            public float Thickness
            {
                get
                {
                    return constants.Thickness;
                }
                set
                {
                    constants.Thickness = MathHelper.Clamp(value, 0.0f, 500.0f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Margin)]
            public float Margin { get; set; }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));

                var inputRect = inputRects[0];
                var margin = (int)Math.Ceiling(Margin);
                outputRect = new RawRect(
                    inputRect.Left - margin,
                    inputRect.Top - margin,
                    inputRect.Right + margin,
                    inputRect.Bottom + margin);
                outputOpaqueSubRect = default;
            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var thickness = (int)Math.Ceiling(Thickness);
                inputRects[0] = new RawRect(outputRect.Left - thickness,
                                                 outputRect.Top - thickness,
                                                 outputRect.Right + thickness,
                                                 outputRect.Bottom + thickness);
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Thickness;
            }
            public enum Properties : int
            {
                Thickness = 0,
                Margin = 1,
            }
        }
    }
}
