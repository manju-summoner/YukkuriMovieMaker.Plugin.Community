using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceJfaSeedCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float TotalRange { set => SetValue((int)EffectImpl.Properties.TotalRange, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;
            private float _totalRange = 300f;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TotalRange)]
            public float TotalRange { get => _totalRange; set => _totalRange = Math.Clamp(value, 1f, RadianceGeometry.MaxRange); }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceJfaSeed"))
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

                var inputRect = ClampInputRect(inputRects[0]);
                outputOpaqueSubRect = default;

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    return;
                }

                var pad = RadianceGeometry.WorldPad(_totalRange);
                outputRect = new RawRect(
                    Saturate((long)inputRect.Left - pad),
                    Saturate((long)inputRect.Top - pad),
                    Saturate((long)inputRect.Right + pad),
                    Saturate((long)inputRect.Bottom + pad));

                _cb.OriginL = outputRect.Left;
                _cb.OriginT = outputRect.Top;
                UpdateConstants();
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = outputRect;
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float OriginL;
                public float OriginT;
                public float Pad0;
                public float Pad1;
            }

            public enum Properties : int
            {
                TotalRange = 0,
            }
        }
    }
}
