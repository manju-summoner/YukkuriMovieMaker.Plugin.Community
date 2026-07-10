using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal sealed class BevelLightingCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 Light { get => GetVector4Value((int)EffectImpl.Properties.Light); set => SetValue((int)EffectImpl.Properties.Light, value); }
        public float SurfaceScale { get => GetFloatValue((int)EffectImpl.Properties.SurfaceScale); set => SetValue((int)EffectImpl.Properties.SurfaceScale, value); }
        public float ReflectionConstant { get => GetFloatValue((int)EffectImpl.Properties.ReflectionConstant); set => SetValue((int)EffectImpl.Properties.ReflectionConstant, value); }
        public float Exponent { get => GetFloatValue((int)EffectImpl.Properties.Exponent); set => SetValue((int)EffectImpl.Properties.Exponent, value); }
        public int LightMode { get => GetIntValue((int)EffectImpl.Properties.LightMode); set => SetValue((int)EffectImpl.Properties.LightMode, value); }
        public int ReflectionMode { get => GetIntValue((int)EffectImpl.Properties.ReflectionMode); set => SetValue((int)EffectImpl.Properties.ReflectionMode, value); }

        [CustomEffect(1)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Light)]
            public Vector4 Light { get => constants.Light; set { constants.Light = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.SurfaceScale)]
            public float SurfaceScale { get => constants.SurfaceScale; set { constants.SurfaceScale = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ReflectionConstant)]
            public float ReflectionConstant { get => constants.ReflectionConstant; set { constants.ReflectionConstant = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Exponent)]
            public float Exponent { get => constants.Exponent; set { constants.Exponent = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.LightMode)]
            public int LightMode { get => constants.LightMode; set { constants.LightMode = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.ReflectionMode)]
            public int ReflectionMode { get => constants.ReflectionMode; set { constants.ReflectionMode = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelLighting")) { }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => new(invalidInputRect.Left - 1, invalidInputRect.Top - 1, invalidInputRect.Right + 1, invalidInputRect.Bottom + 1);

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = new RawRect(outputRect.Left - 1, outputRect.Top - 1, outputRect.Right + 1, outputRect.Bottom + 1);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 Light;
                public float SurfaceScale;
                public float ReflectionConstant;
                public float Exponent;
                public int LightMode;
                public int ReflectionMode;
            }

            public enum Properties
            {
                Light,
                SurfaceScale,
                ReflectionConstant,
                Exponent,
                LightMode,
                ReflectionMode,
            }
        }
    }
}
