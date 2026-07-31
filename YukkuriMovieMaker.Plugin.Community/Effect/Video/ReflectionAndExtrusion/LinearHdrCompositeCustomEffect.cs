using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion
{
    internal sealed class LinearHdrCompositeCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Project.Blend BlendMode
        {
            get => (Project.Blend)GetIntValue((int)EffectImpl.Properties.BlendMode);
            set => SetValue((int)EffectImpl.Properties.BlendMode, (int)value);
        }

        public void SetBaseInput(ID2D1Image? input) => SetInput(0, input, true);
        public void SetReflectionInput(ID2D1Image? input) => SetInput(1, input, true);

        [CustomEffect(2)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.BlendMode)]
            public int BlendMode
            {
                get => constants.BlendMode;
                set { constants.BlendMode = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("LinearHdrComposite"))
            {
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = inputRects[1] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public int BlendMode;
            }

            public enum Properties
            {
                BlendMode,
            }
        }
    }
}
