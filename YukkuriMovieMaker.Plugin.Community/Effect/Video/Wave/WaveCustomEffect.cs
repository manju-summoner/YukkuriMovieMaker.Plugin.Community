using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Wave
{
    public class WaveCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle1
        {
            set => SetValue((int)EffectImpl.Properties.Angle1, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle1);
        }
        public float Angle2
        {
            set => SetValue((int)EffectImpl.Properties.Angle2, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle2);
        }
        public float Amplitude
        {
            set => SetValue((int)EffectImpl.Properties.Amplitude, value);
            get => GetFloatValue((int)EffectImpl.Properties.Amplitude);
        }
        public float Length
        {
            set => SetValue((int)EffectImpl.Properties.Length, value);
            get => GetFloatValue((int)EffectImpl.Properties.Length);
        }
        public float Phase
        {
            set => SetValue((int)EffectImpl.Properties.Phase, value);
            get => GetFloatValue((int)EffectImpl.Properties.Phase);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle1)]
            public float Angle1
            {
                set
                {
                    constants.Angle1 = value;
                    UpdateConstants();
                }
                get => constants.Angle1;
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle2)]
            public float Angle2
            {
                set
                {
                    constants.Angle2 = value;
                    UpdateConstants();
                }
                get => constants.Angle2;
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Length)]
            public float Length
            {
                set
                {
                    constants.Length = value;
                    UpdateConstants();
                }
                get => constants.Length;
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

            public EffectImpl() : base(ShaderResourceUri.Get("Wave"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputOpaqueSubRect = default;

                var angle = Angle1 + Angle2;
                var v = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Amplitude;

                var rangeX = (int)MathF.Ceiling(MathF.Abs(v.X));
                var rangeY = (int)MathF.Ceiling(MathF.Abs(v.Y));

                var input = inputRects[0];
                outputRect = new RawRect(
                    input.Left - rangeX,
                    input.Top - rangeY,
                    input.Right + rangeX,
                    input.Bottom + rangeY);
            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var angle = Angle1 + Angle2;
                var v = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Amplitude;

                var rangeX = (int)MathF.Ceiling(MathF.Abs(v.X));
                var rangeY = (int)MathF.Ceiling(MathF.Abs(v.Y));

                inputRects[0] = new RawRect(
                    outputRect.Left - rangeX,
                    outputRect.Top - rangeY,
                    outputRect.Right + rangeX,
                    outputRect.Bottom + rangeY);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Angle1;
                public float Angle2;
                public float Amplitude;
                public float Length;
                public float Phase;
            }
            public enum Properties : int
            {
                Angle1 = 0,
                Angle2 = 1,
                Amplitude = 2,
                Length = 3,
                Phase = 4,
            }
        }
    }
}
