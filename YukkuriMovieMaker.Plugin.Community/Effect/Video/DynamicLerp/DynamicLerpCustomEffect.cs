using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DynamicLerp
{
    internal sealed class DynamicLerpCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int WeightSource
        {
            get => GetIntValue((int)EffectImpl.Properties.WeightSource);
            set => SetValue((int)EffectImpl.Properties.WeightSource, value);
        }

        public void SetCurrentInput(ID2D1Image? image) => SetInput(0, image, true);
        public void SetTargetInput(ID2D1Image? image) => SetInput(1, image, true);
        public void SetMapInput(ID2D1Image? image) => SetInput(2, image, true);

        [CustomEffect(3)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.WeightSource)]
            public int WeightSource
            {
                get => _cb.WeightSource;
                set { _cb.WeightSource = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("DynamicLerp")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(
                RawRect[] inputRects,
                RawRect[] inputOpaqueSubRects,
                out RawRect outputRect,
                out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects.Length > 0 ? inputRects[0] : default;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(
                RawRect outputRect,
                RawRect[] inputRects)
            {
                for (int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public int WeightSource;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }

            public enum Properties
            {
                WeightSource = 0,
            }
        }
    }
}
