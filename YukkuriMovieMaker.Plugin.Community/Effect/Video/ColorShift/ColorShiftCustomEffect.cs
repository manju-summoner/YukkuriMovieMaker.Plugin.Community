using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorShift
{
    public class ColorShiftCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Distance
        {
            set => SetValue((int)EffectImpl.Properties.Distance, value);
            get => GetFloatValue((int)EffectImpl.Properties.Distance);
        }
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        public float Strength
        {
            set => SetValue((int)EffectImpl.Properties.Strength, value);
            get => GetFloatValue((int)EffectImpl.Properties.Strength);
        }
        public int Mode
        {
            set => SetValue((int)EffectImpl.Properties.Mode, value);
            get => GetIntValue((int)EffectImpl.Properties.Mode);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Distance)]
            public float Distance
            {
                get
                {
                    return constants.Distance;
                }
                set
                {
                    constants.Distance = Math.Max(value, 0);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Angle
            {
                get
                {
                    return constants.Angle;
                }
                set
                {
                    constants.Angle = value % 360;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Strength
            {
                get
                {
                    return constants.Strength;
                }
                set
                {
                    constants.Strength = Math.Clamp(value, 0, 1);
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

            public EffectImpl() : base(ShaderResourceUri.Get("ColorShift"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var vector = new System.Numerics.Vector2(0, -Distance);
                var rotated = System.Numerics.Vector2.Transform(vector, System.Numerics.Matrix3x2.CreateRotation(MathF.PI * Angle / 180f));
                var w = (int)Math.Ceiling(Math.Abs(rotated.X));
                var h = (int)Math.Ceiling(Math.Abs(rotated.Y));

                inputRect = inputRects[0];
                outputRect = new RawRect(
                    inputRect.Left - w,
                    inputRect.Top - h,
                    inputRect.Right + w,
                    inputRect.Bottom + h);
                outputOpaqueSubRect = default;

            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var vector = new System.Numerics.Vector2(0, -Distance);
                var rotated = System.Numerics.Vector2.Transform(vector, System.Numerics.Matrix3x2.CreateRotation(MathF.PI * Angle / 180f));
                var w = (int)Math.Ceiling(Math.Abs(rotated.X));
                var h = (int)Math.Ceiling(Math.Abs(rotated.Y));

                inputRects[0] = new RawRect(
                    outputRect.Left - w,
                    outputRect.Top - h,
                    outputRect.Right + w,
                    outputRect.Bottom + h);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Distance;
                public float Angle;
                public float Strength;
                public int Mode;
            }
            public enum Properties : int
            {
                Distance = 0,
                Angle = 1,
                Strength = 2,
                Mode = 3,
            }
        }
    }
}
