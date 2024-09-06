using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LongShadow
{
    public class LongShadowCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        public float Length
        {
            set => SetValue((int)EffectImpl.Properties.Length, value);
            get => GetFloatValue((int)EffectImpl.Properties.Length);
        }
        public float Opacity
        {
            set => SetValue((int)EffectImpl.Properties.Opacity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Opacity);
        }
        public float Attenuation
        {
            set => SetValue((int)EffectImpl.Properties.Attenuation, value);
            get => GetFloatValue((int)EffectImpl.Properties.Attenuation);
        }
        public Vector4 Color1
        {
            set => SetValue((int)EffectImpl.Properties.Color1, value);
            get => GetVector4Value((int)EffectImpl.Properties.Color1);
        }
        public Vector4 Color2
        {
            set => SetValue((int)EffectImpl.Properties.Color2, value);
            get => GetVector4Value((int)EffectImpl.Properties.Color2);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Length)]
            public float Length
            {
                get
                {
                    return constants.Length;
                }
                set
                {
                    constants.Length = Math.Max(value, 0);
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
                    constants.Angle = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Opacity)]
            public float Opacity
            {
                get
                {
                    return constants.Opacity;
                }
                set
                {
                    constants.Opacity = Math.Clamp(value, 0, 1);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Attenuation)]
            public float Attenuation
            {
                get
                {
                    return constants.Attenuation;
                }
                set
                {
                    constants.Attenuation = Math.Clamp(value, 0, 1);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Color1)]
            public Vector4 Color1
            {
                get
                {
                    return constants.Color1;
                }
                set
                {
                    constants.Color1 = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Color2)]
            public Vector4 Color2
            {
                get
                {
                    return constants.Color2;
                }
                set
                {
                    constants.Color2 = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("LongShadow"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];
                var vector =
                    Vector2.Transform(
                        new Vector2(0, -Length),
                        Matrix3x2.CreateRotation(Angle));

                var shadowEndRect =
                    new RawRect(
                        inputRect.Left + (int)MathF.Floor(vector.X),
                        inputRect.Top + (int)MathF.Floor(vector.Y),
                        inputRect.Right + (int)MathF.Ceiling(vector.X),
                        inputRect.Bottom + (int)MathF.Ceiling(vector.Y));

                outputRect = new RawRect(
                    Math.Min(inputRect.Left, shadowEndRect.Left),
                    Math.Min(inputRect.Top, shadowEndRect.Top),
                    Math.Max(inputRect.Right, shadowEndRect.Right),
                    Math.Max(inputRect.Bottom, shadowEndRect.Bottom));
                outputOpaqueSubRect = default;

            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var vector =
                    Vector2.Transform(
                        new Vector2(0, Length),
                        Matrix3x2.CreateRotation(Angle));

                var shadowEndRect =
                    new RawRect(
                        outputRect.Left + (int)MathF.Floor(vector.X),
                        outputRect.Top + (int)MathF.Floor(vector.Y),
                        outputRect.Right + (int)MathF.Ceiling(vector.X),
                        outputRect.Bottom + (int)MathF.Ceiling(vector.Y));

                var inputRect = new RawRect(
                    Math.Min(outputRect.Left, shadowEndRect.Left),
                    Math.Min(outputRect.Top, shadowEndRect.Top),
                    Math.Max(outputRect.Right, shadowEndRect.Right),
                    Math.Max(outputRect.Bottom, shadowEndRect.Bottom));

                inputRects[0] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Angle;
                public float Length;
                public float Opacity;
                public float Attenuation;
                public Vector4 Color1;
                public Vector4 Color2;
            }
            public enum Properties : int
            {
                Angle = 0,
                Length = 1,
                Opacity = 2,
                Attenuation = 3,
                Color1 = 4,
                Color2 = 5,
            }
        }
    }
}
