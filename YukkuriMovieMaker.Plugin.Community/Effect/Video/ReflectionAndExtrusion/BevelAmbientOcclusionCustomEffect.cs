using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion
{
    internal sealed class BevelAmbientOcclusionCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Strength { get => GetFloatValue((int)Properties.Strength); set => SetValue((int)Properties.Strength, value); }
        public float Distance { get => GetFloatValue((int)Properties.Distance); set => SetValue((int)Properties.Distance, value); }
        public float Bias { get => GetFloatValue((int)Properties.Bias); set => SetValue((int)Properties.Bias, value); }
        public float Softness { get => GetFloatValue((int)Properties.Softness); set => SetValue((int)Properties.Softness, value); }
        public float SurfaceScale { get => GetFloatValue((int)Properties.SurfaceScale); set => SetValue((int)Properties.SurfaceScale, value); }
        public OcclusionQuality Quality { get => (OcclusionQuality)GetIntValue((int)Properties.StepCount); set => SetValue((int)Properties.StepCount, (int)value); }

        enum Properties { Strength, Distance, Bias, Softness, SurfaceScale, StepCount }

        [CustomEffect(2)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants = new() { StepCount = 4 };
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Strength)] public float Strength { get => constants.Strength; set { constants.Strength = Math.Clamp(value, 0, 1); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Distance)] public float Distance { get => constants.Distance; set { constants.Distance = Math.Clamp(value, 0, 256); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Bias)] public float Bias { get => constants.Bias; set { constants.Bias = Math.Clamp(value, 0, 64); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Softness)] public float Softness { get => constants.Softness; set { constants.Softness = Math.Clamp(value, 0, 64); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.SurfaceScale)] public float SurfaceScale { get => constants.SurfaceScale; set { constants.SurfaceScale = Math.Max(value, 0); UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.StepCount)] public int StepCount { get => constants.StepCount; set { constants.StepCount = Math.Clamp(value, 4, 8); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelAmbientOcclusion")) { }
            public override void SetDrawInfo(ID2D1DrawInfo drawInfo) { base.SetDrawInfo(drawInfo); drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four); }
            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);
            int Padding() => constants.Strength > 0 ? (int)Math.Ceiling(constants.Distance) + 1 : 0;
            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => inputIndex == 1 ? Inflate(invalidInputRect, Padding()) : invalidInputRect;
            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect) { outputRect = inputRects[0]; outputOpaqueSubRect = default; }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects) { inputRects[0] = outputRect; inputRects[1] = Inflate(outputRect, Padding()); }
            static RawRect Inflate(RawRect rect, int amount) => new(Saturate((long)rect.Left - amount), Saturate((long)rect.Top - amount), Saturate((long)rect.Right + amount), Saturate((long)rect.Bottom + amount));
            static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer { public float Strength; public float Distance; public float Bias; public float Softness; public float SurfaceScale; public int StepCount; public float Pad0; public float Pad1; }
        }
    }
}
