using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceCompositeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Strength { set => SetValue((int)EffectImpl.Properties.Strength, value); }
        public float Diffuse { set => SetValue((int)EffectImpl.Properties.Diffuse, value); }
        public float Ambient { set => SetValue((int)EffectImpl.Properties.Ambient, value); }
        public float RangePx { set => SetValue((int)EffectImpl.Properties.RangePx, value); }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb = new() { Strength = 1f, Diffuse = 0.6f, Ambient = 1f };
            private float _rangePx = 300f;
            private RawRect _atlasRect;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Clamp(value, 0f, 8f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Diffuse)]
            public float Diffuse { get => _cb.Diffuse; set { _cb.Diffuse = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Ambient)]
            public float Ambient { get => _cb.Ambient; set { _cb.Ambient = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.RangePx)]
            public float RangePx { get => _rangePx; set => _rangePx = Math.Clamp(value, 1f, RadianceGeometry.MaxRange); }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceComposite"))
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

                var inputRect = ClampInputRect(inputRects[0]);
                outputOpaqueSubRect = default;

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    return;
                }

                var pad = RadianceGeometry.WorldPad(_rangePx);
                var worldL = (long)inputRect.Left - pad;
                var worldT = (long)inputRect.Top - pad;
                var worldW = (long)inputRect.Right - inputRect.Left + pad * 2;
                var worldH = (long)inputRect.Bottom - inputRect.Top + pad * 2;

                var probeW = RadianceGeometry.ProbeCount((int)worldW, 0);
                var probeH = RadianceGeometry.ProbeCount((int)worldH, 0);

                _cb.WorldL = worldL;
                _cb.WorldT = worldT;
                _cb.ProbeW = probeW;
                _cb.ProbeH = probeH;
                UpdateConstants();

                outputRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + worldW),
                    Saturate(worldT + worldH));

                var tiles = RadianceGeometry.TilesSide(0);
                _atlasRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + (long)tiles * probeW),
                    Saturate(worldT + (long)tiles * probeH));
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - 2),
                    Saturate((long)outputRect.Top - 2),
                    Saturate((long)outputRect.Right + 2),
                    Saturate((long)outputRect.Bottom + 2));
                inputRects[1] = _atlasRect;
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Strength;
                public float Diffuse;
                public float Ambient;
                public float Pad0;
                public float WorldL;
                public float WorldT;
                public float ProbeW;
                public float ProbeH;
            }

            public enum Properties : int
            {
                Strength = 0,
                Diffuse = 1,
                Ambient = 2,
                RangePx = 3,
            }
        }
    }
}
