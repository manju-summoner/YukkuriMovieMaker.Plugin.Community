using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceOccupancyCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int BuildLevel { set => SetValue((int)EffectImpl.Properties.BuildLevel, value); }
        public float TotalRange { set => SetValue((int)EffectImpl.Properties.TotalRange, value); }
        public float Gain { set => SetValue((int)EffectImpl.Properties.Gain, value); }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb = new() { BuildLevel = 1f, Gain = 1.5f };
            private float _totalRange = 300f;
            private RawRect _worldRect;
            private RawRect _atlasRect;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.BuildLevel)]
            public int BuildLevel { get => (int)_cb.BuildLevel; set { _cb.BuildLevel = Math.Clamp(value, 1, RadianceGeometry.OccLevelCount); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TotalRange)]
            public float TotalRange { get => _totalRange; set => _totalRange = Math.Clamp(value, 1f, RadianceGeometry.MaxRange); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Gain)]
            public float Gain { get => _cb.Gain; set { _cb.Gain = Math.Clamp(value, 0f, 16f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceOccupancy"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 2)
                    throw new ArgumentException("InputRects must be length of 2", nameof(inputRects));

                var inputRect = ClampInputRect(inputRects[1]);
                outputOpaqueSubRect = default;

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    _atlasRect = inputRect;
                    return;
                }

                var pad = RadianceGeometry.WorldPad(_totalRange);
                var worldL = (long)inputRect.Left - pad;
                var worldT = (long)inputRect.Top - pad;
                var worldW = (long)inputRect.Right - inputRect.Left + pad * 2;
                var worldH = (long)inputRect.Bottom - inputRect.Top + pad * 2;

                _cb.WorldL = worldL;
                _cb.WorldT = worldT;
                _cb.WorldW = worldW;
                _cb.WorldH = worldH;
                UpdateConstants();

                _worldRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + worldW),
                    Saturate(worldT + worldH));

                outputRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + RadianceGeometry.OccAtlasWidth((int)worldW)),
                    Saturate(worldT + RadianceGeometry.OccAtlasHeight((int)worldH)));
                _atlasRect = outputRect;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = _atlasRect;
                inputRects[1] = _worldRect;
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float WorldL;
                public float WorldT;
                public float WorldW;
                public float WorldH;
                public float BuildLevel;
                public float Gain;
                public float Pad0;
                public float Pad1;
            }

            public enum Properties : int
            {
                BuildLevel = 0,
                TotalRange = 1,
                Gain = 2,
            }
        }
    }
}
