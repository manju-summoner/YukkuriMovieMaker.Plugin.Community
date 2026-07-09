using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceCascadeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float IntervalStart { set => SetValue((int)EffectImpl.Properties.IntervalStart, value); }
        public float IntervalEnd { set => SetValue((int)EffectImpl.Properties.IntervalEnd, value); }
        public float Phase { set => SetValue((int)EffectImpl.Properties.Phase, value); }
        public int IsTop { set => SetValue((int)EffectImpl.Properties.IsTop, value); }
        public float TotalRange { set => SetValue((int)EffectImpl.Properties.TotalRange, value); }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const float MaxRange = 4096f;

            private ConstantBuffer _cb;
            private float _totalRange = 300f;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.IntervalStart)]
            public float IntervalStart { get => _cb.IntervalStart; set { _cb.IntervalStart = Math.Clamp(value, 0f, MaxRange); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.IntervalEnd)]
            public float IntervalEnd { get => _cb.IntervalEnd; set { _cb.IntervalEnd = Math.Clamp(value, 0f, MaxRange); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Phase)]
            public float Phase { get => _cb.Phase; set { _cb.Phase = Math.Clamp(value, -100f, 100f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.IsTop)]
            public int IsTop { get => (int)_cb.IsTop; set { _cb.IsTop = value != 0 ? 1f : 0f; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TotalRange)]
            public float TotalRange { get => _totalRange; set { _totalRange = Math.Clamp(value, 1f, MaxRange); } }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceCascade"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            private int OutputPadding() => (int)MathF.Ceiling(_totalRange) + 2;

            private int ReadPadding() => (int)MathF.Ceiling(_cb.IntervalEnd * 1.5f) + 2;

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 2)
                    throw new ArgumentException("InputRects must be length of 2", nameof(inputRects));

                var inputRect = ClampInputRect(inputRects[0]);
                outputOpaqueSubRect = default;

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    return;
                }

                var pad = OutputPadding();
                outputRect = new RawRect(
                    Saturate((long)inputRect.Left - pad),
                    Saturate((long)inputRect.Top - pad),
                    Saturate((long)inputRect.Right + pad),
                    Saturate((long)inputRect.Bottom + pad));
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var pad = ReadPadding();
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - pad),
                    Saturate((long)outputRect.Top - pad),
                    Saturate((long)outputRect.Right + pad),
                    Saturate((long)outputRect.Bottom + pad));
                inputRects[1] = new RawRect(
                    Saturate((long)outputRect.Left - 2),
                    Saturate((long)outputRect.Top - 2),
                    Saturate((long)outputRect.Right + 2),
                    Saturate((long)outputRect.Bottom + 2));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float IntervalStart;
                public float IntervalEnd;
                public float Phase;
                public float IsTop;
            }

            public enum Properties : int
            {
                IntervalStart = 0,
                IntervalEnd = 1,
                Phase = 2,
                IsTop = 3,
                TotalRange = 4,
            }
        }
    }
}
