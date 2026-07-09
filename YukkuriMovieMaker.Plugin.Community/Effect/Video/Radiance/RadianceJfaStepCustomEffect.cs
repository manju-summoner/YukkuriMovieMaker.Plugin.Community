using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceJfaStepCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float StepPx { set => SetValue((int)EffectImpl.Properties.StepPx, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb = new() { StepPx = 1f };

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StepPx)]
            public float StepPx { get => _cb.StepPx; set { _cb.StepPx = Math.Clamp(value, 1f, 4096f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceJfaStep"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));

                outputRect = ClampInputRect(inputRects[0]);
                outputOpaqueSubRect = default;

                _cb.OriginL = outputRect.Left;
                _cb.OriginT = outputRect.Top;
                UpdateConstants();
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var pad = (int)MathF.Ceiling(_cb.StepPx) + 1;
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - pad),
                    Saturate((long)outputRect.Top - pad),
                    Saturate((long)outputRect.Right + pad),
                    Saturate((long)outputRect.Bottom + pad));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float StepPx;
                public float OriginL;
                public float OriginT;
                public float Pad0;
            }

            public enum Properties : int
            {
                StepPx = 0,
            }
        }
    }
}
