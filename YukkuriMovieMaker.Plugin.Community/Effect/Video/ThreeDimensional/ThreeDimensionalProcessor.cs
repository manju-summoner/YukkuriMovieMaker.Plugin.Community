using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ThreeDimensional
{
    public class ThreeDimensionalProcessor(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
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
        public Vector2 Center
        {
            set => SetValue((int)EffectImpl.Properties.Center, value);
            get => GetVector2Value((int)EffectImpl.Properties.Center);
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

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

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
            [CustomEffectProperty(PropertyType.Vector2, (int)Properties.Center)]
            public Vector2 Center
            {
                get
                {
                    return constants.Center;
                }
                set
                {
                    constants.Center = value;
                    UpdateConstants();
                }
            }

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

            public EffectImpl() : base(ShaderResourceUri.Get("ThreeDimensional"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];
                var shadowEndRect =
                    new RawRect(
                        (int)Math.Floor((inputRect.Left - Center.X) * (1 - Length) + Center.X),
                        (int)Math.Floor((inputRect.Top - Center.Y) * (1 - Length) + Center.Y),
                        (int)Math.Ceiling((inputRect.Right - Center.X) * (1 - Length) + Center.X),
                        (int)Math.Ceiling((inputRect.Bottom - Center.Y) * (1 - Length) + Center.Y));

                outputRect = new RawRect(
                    Math.Min(inputRect.Left, shadowEndRect.Left),
                    Math.Min(inputRect.Top, shadowEndRect.Top),
                    Math.Max(inputRect.Right, shadowEndRect.Right),
                    Math.Max(inputRect.Bottom, shadowEndRect.Bottom));
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var vectors = new[]
                {
                    new Vector2(outputRect.Left,outputRect.Top),
                    new Vector2(outputRect.Right, outputRect.Top),
                    new Vector2(outputRect.Left,outputRect.Bottom),
                    new Vector2(outputRect.Right, outputRect.Bottom)
                };
                var inputVectors = vectors.Select(GetInputPoint).ToArray();

                var shadowEndRect = new RawRect(
                    (int)Math.Floor(inputVectors.Select(v => v.X).Min()),
                    (int)Math.Floor(inputVectors.Select(v => v.Y).Min()),
                    (int)Math.Ceiling(inputVectors.Select(v => v.X).Max()),
                    (int)Math.Ceiling(inputVectors.Select(v => v.Y).Max()));

                inputRects[0] = new RawRect(
                    Math.Min(outputRect.Left, shadowEndRect.Left),
                    Math.Min(outputRect.Top, shadowEndRect.Top),
                    Math.Max(outputRect.Right, shadowEndRect.Right),
                    Math.Max(outputRect.Bottom, shadowEndRect.Bottom));
            }
            Vector2 GetInputPoint(Vector2 outputPoint) => (outputPoint - Center) / (1 - Length) + Center;


            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 Color1;
                public Vector4 Color2;
                public Vector2 Center;
                public float Length;
                public float Opacity;
                public float Attenuation;
            }
            public enum Properties : int
            {
                Color1 = 0,
                Color2 = 1,
                Center = 2,
                Length = 3,
                Opacity = 4,
                Attenuation = 5,
            }
        }
    }
}
