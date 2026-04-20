using System;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InnerOutline
{
    public class InnerOutlineCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        static readonly Uri shaderUri = new("pack://application:,,,/YukkuriMovieMaker;component/Resources/Shader/OutlineLite.cso");

        public float StepPx
        {
            set => SetValue((int)EffectImpl.Properties.StepPx, value);
            get => GetFloatValue((int)EffectImpl.Properties.StepPx);
        }

        public int Samples
        {
            set => SetValue((int)EffectImpl.Properties.Samples, value);
            get => GetIntValue((int)EffectImpl.Properties.Samples);
        }

        public bool IsAngular
        {
            set => SetValue((int)EffectImpl.Properties.IsAngular, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsAngular);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            public EffectImpl() : base(shaderUri)
            {
                constants.StepPx = 0f;
                constants.Samples = 16;
                constants.IsAngular = false;
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StepPx)]
            public float StepPx
            {
                get => constants.StepPx;
                set
                {
                    constants.StepPx = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Samples)]
            public int Samples
            {
                get => constants.Samples;
                set
                {
                    constants.Samples = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsAngular)]
            public bool IsAngular
            {
                get => constants.IsAngular;
                set
                {
                    constants.IsAngular = value;
                    UpdateConstants();
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));

                var inputRect = inputRects[0];
                var expand = (int)Math.Ceiling(constants.StepPx);
                outputRect = new RawRect(
                    inputRect.Left - expand,
                    inputRect.Top - expand,
                    inputRect.Right + expand,
                    inputRect.Bottom + expand);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var expand = (int)Math.Ceiling(constants.StepPx);
                inputRects[0] = new RawRect(
                    outputRect.Left - expand,
                    outputRect.Top - expand,
                    outputRect.Right + expand,
                    outputRect.Bottom + expand);
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float StepPx;
                public int Samples;
                public bool IsAngular;
                public float Padding1;
            }

            public enum Properties : int
            {
                StepPx = 0,
                Samples = 1,
                IsAngular = 2,
            }
        }
    }
}
