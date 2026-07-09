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
        public int Level { set => SetValue((int)EffectImpl.Properties.Level, value); }
        public float IntervalStart { set => SetValue((int)EffectImpl.Properties.IntervalStart, value); }
        public float IntervalEnd { set => SetValue((int)EffectImpl.Properties.IntervalEnd, value); }
        public float TotalRange { set => SetValue((int)EffectImpl.Properties.TotalRange, value); }

        [CustomEffect(3)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;
            private int _level;
            private float _totalRange = 300f;
            private RawRect _emissionReadRect;
            private RawRect _upperAtlasRect;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Level)]
            public int Level { get => _level; set => _level = Math.Clamp(value, 0, RadianceGeometry.LevelCount - 1); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.IntervalStart)]
            public float IntervalStart { get => _cb.IntervalStart; set { _cb.IntervalStart = Math.Clamp(value, 0f, RadianceGeometry.MaxRange); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.IntervalEnd)]
            public float IntervalEnd { get => _cb.IntervalEnd; set { _cb.IntervalEnd = Math.Clamp(value, 0f, RadianceGeometry.MaxRange); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TotalRange)]
            public float TotalRange { get => _totalRange; set => _totalRange = Math.Clamp(value, 1f, RadianceGeometry.MaxRange); }

            public EffectImpl() : base(ShaderResourceUri.Get("RadianceCascade"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 3)
                    throw new ArgumentException("InputRects must be length of 3", nameof(inputRects));

                var inputRect = ClampInputRect(inputRects[0]);
                outputOpaqueSubRect = default;

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    return;
                }

                var pad = RadianceGeometry.WorldPad(_totalRange);
                var worldL = (long)inputRect.Left - pad;
                var worldT = (long)inputRect.Top - pad;
                var worldW = (long)inputRect.Right - inputRect.Left + pad * 2;
                var worldH = (long)inputRect.Bottom - inputRect.Top + pad * 2;

                var tiles = RadianceGeometry.TilesSide(_level);
                var probeW = RadianceGeometry.ProbeCount((int)worldW, _level);
                var probeH = RadianceGeometry.ProbeCount((int)worldH, _level);

                _cb.WorldL = worldL;
                _cb.WorldT = worldT;
                _cb.Spacing = RadianceGeometry.Spacing(_level);
                _cb.TilesSide = tiles;
                _cb.ProbeW = probeW;
                _cb.ProbeH = probeH;
                _cb.IsTop = _level == RadianceGeometry.LevelCount - 1 ? 1f : 0f;

                var upLevel = Math.Min(_level + 1, RadianceGeometry.LevelCount - 1);
                var upProbeW = RadianceGeometry.ProbeCount((int)worldW, upLevel);
                var upProbeH = RadianceGeometry.ProbeCount((int)worldH, upLevel);
                _cb.UpProbeW = upProbeW;
                _cb.UpProbeH = upProbeH;
                UpdateConstants();

                outputRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + (long)tiles * probeW),
                    Saturate(worldT + (long)tiles * probeH));

                var readPad = (int)MathF.Ceiling(_cb.IntervalEnd * 1.7f) + 2;
                _emissionReadRect = new RawRect(
                    Saturate(worldL - readPad),
                    Saturate(worldT - readPad),
                    Saturate(worldL + worldW + readPad),
                    Saturate(worldT + worldH + readPad));

                var upTiles = RadianceGeometry.TilesSide(upLevel);
                _upperAtlasRect = new RawRect(
                    Saturate(worldL),
                    Saturate(worldT),
                    Saturate(worldL + (long)upTiles * upProbeW),
                    Saturate(worldT + (long)upTiles * upProbeH));
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = _emissionReadRect;
                inputRects[1] = _upperAtlasRect;
                inputRects[2] = _emissionReadRect;
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float WorldL;
                public float WorldT;
                public float Spacing;
                public float TilesSide;
                public float ProbeW;
                public float ProbeH;
                public float IntervalStart;
                public float IntervalEnd;
                public float UpProbeW;
                public float UpProbeH;
                public float IsTop;
                public float Pad0;
            }

            public enum Properties : int
            {
                Level = 0,
                IntervalStart = 1,
                IntervalEnd = 2,
                TotalRange = 3,
            }
        }
    }
}
