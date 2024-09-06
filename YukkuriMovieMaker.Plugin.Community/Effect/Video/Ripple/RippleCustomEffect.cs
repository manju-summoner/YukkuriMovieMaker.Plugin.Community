using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Ripple
{
    public class RippleCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float WaveLength
        {
            set => SetValue((int)EffectImpl.Properties.WaveLength, value);
            get => GetFloatValue((int)EffectImpl.Properties.WaveLength);
        }
        public float Phase
        {
            set => SetValue((int)EffectImpl.Properties.Phase, value);
            get => GetFloatValue((int)EffectImpl.Properties.Phase);
        }
        public float Amplitude
        {
            set => SetValue((int)EffectImpl.Properties.Amplitude, value);
            get => GetFloatValue((int)EffectImpl.Properties.Amplitude);
        }
        public float X
        {
            set => SetValue((int)EffectImpl.Properties.X, value);
            get => GetFloatValue((int)EffectImpl.Properties.X);
        }
        public float Y
        {
            set => SetValue((int)EffectImpl.Properties.Y, value);
            get => GetFloatValue((int)EffectImpl.Properties.Y);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;


            [CustomEffectProperty(PropertyType.Float, (int)Properties.WaveLength)]
            public float WaveLength
            {
                set
                {
                    constants.WaveLength = value;
                    UpdateConstants();
                }
                get => constants.WaveLength;
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Phase)]
            public float Phase
            {
                set
                {
                    constants.Phase = value;
                    UpdateConstants();
                }
                get => constants.Phase;
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Amplitude)]
            public float Amplitude
            {
                set
                {
                    constants.Amplitude = value;
                    UpdateConstants();
                }
                get => constants.Amplitude;
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.X)]
            public float X
            {
                set
                {
                    constants.X = value;
                    UpdateConstants();
                }
                get => constants.X;
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Y)]
            public float Y
            {
                set
                {
                    constants.Y = value;
                    UpdateConstants();
                }
                get => constants.Y;
            }

            public EffectImpl() : base(ShaderResourceUri.Get("Ripple"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));
                outputOpaqueSubRect = default;
                outputRect = inputRects[0];
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                int expansion = (int)Math.Abs(Math.Round(constants.Amplitude));
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));
                inputRects[0] = new RawRect(
                    outputRect.Left - expansion,
                    outputRect.Top - expansion,
                    outputRect.Right + expansion,
                    outputRect.Bottom + expansion);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float WaveLength;
                public float Phase;
                public float Amplitude;
                public float X;
                public float Y;
            }
            public enum Properties : int
            {
                WaveLength = 0,
                Phase = 1,
                Amplitude = 2,
                X = 3,
                Y = 4,
            }
        }

    }
}
