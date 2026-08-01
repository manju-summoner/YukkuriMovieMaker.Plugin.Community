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
        public float ShadowStrength { get => GetFloatValue((int)EffectImpl.Properties.ShadowStrength); set => SetValue((int)EffectImpl.Properties.ShadowStrength, value); }
        public float ShadowDistance { get => GetFloatValue((int)EffectImpl.Properties.ShadowDistance); set => SetValue((int)EffectImpl.Properties.ShadowDistance, value); }
        public float ShadowBias { get => GetFloatValue((int)EffectImpl.Properties.ShadowBias); set => SetValue((int)EffectImpl.Properties.ShadowBias, value); }
        public float ShadowSoftness { get => GetFloatValue((int)EffectImpl.Properties.ShadowSoftness); set => SetValue((int)EffectImpl.Properties.ShadowSoftness, value); }
        public OcclusionQuality ShadowQuality { get => (OcclusionQuality)GetIntValue((int)EffectImpl.Properties.ShadowStepCount); set => SetValue((int)EffectImpl.Properties.ShadowStepCount, (int)value); }

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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowStrength)]
            public float ShadowStrength { get => constants.ShadowStrength; set { constants.ShadowStrength = Math.Clamp(value, 0, 1); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowDistance)]
            public float ShadowDistance { get => constants.ShadowDistance; set { constants.ShadowDistance = Math.Clamp(value, 0, 256); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowBias)]
            public float ShadowBias { get => constants.ShadowBias; set { constants.ShadowBias = Math.Clamp(value, 0, 64); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowSoftness)]
            public float ShadowSoftness { get => constants.ShadowSoftness; set { constants.ShadowSoftness = Math.Clamp(value, 0, 64); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.ShadowStepCount)]
            public int ShadowStepCount { get => constants.ShadowStepCount; set { constants.ShadowStepCount = Math.Clamp(value, 4, 8); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelLighting")) { constants.ShadowStepCount = 4; }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => Inflate(invalidInputRect, Padding());

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = Inflate(outputRect, Padding());
            }

            int Padding() => constants.ShadowStrength > 0 ? (int)Math.Ceiling(constants.ShadowDistance) + 1 : 1;
            static RawRect Inflate(RawRect rect, int amount) => new(Saturate((long)rect.Left - amount), Saturate((long)rect.Top - amount), Saturate((long)rect.Right + amount), Saturate((long)rect.Bottom + amount));
            static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 Light;
                public float SurfaceScale;
                public float ReflectionConstant;
                public float Exponent;
                public int LightMode;
                public int ReflectionMode;
                public float ShadowStrength;
                public float ShadowDistance;
                public float ShadowBias;
                public float ShadowSoftness;
                public int ShadowStepCount;
            }

            public enum Properties
            {
                Light,
                SurfaceScale,
                ReflectionConstant,
                Exponent,
                LightMode,
                ReflectionMode,
                ShadowStrength,
                ShadowDistance,
                ShadowBias,
                ShadowSoftness,
                ShadowStepCount,
            }
        }
    }
}
