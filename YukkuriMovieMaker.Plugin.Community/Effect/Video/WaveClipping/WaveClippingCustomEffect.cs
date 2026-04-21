using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    public sealed class WaveClippingCustomEffect : D2D1CustomShaderEffectBase
    {
        private enum PropertyIndex
        {
            InputLeft = 0,
            InputTop,
            InputWidth,
            InputHeight,
            Amplitude,
            Frequency,
            Phase,
            EdgePosition,
            BandWidth,
            Softness,
            Mode,
            IsInverted,
            Rotation,
        }

        public float InputLeft { set => SetValue((int)PropertyIndex.InputLeft, value); }
        public float InputTop { set => SetValue((int)PropertyIndex.InputTop, value); }
        public float InputWidth { set => SetValue((int)PropertyIndex.InputWidth, value); }
        public float InputHeight { set => SetValue((int)PropertyIndex.InputHeight, value); }
        public float Amplitude { set => SetValue((int)PropertyIndex.Amplitude, value); }
        public float Frequency { set => SetValue((int)PropertyIndex.Frequency, value); }
        public float Phase { set => SetValue((int)PropertyIndex.Phase, value); }
        public float EdgePosition { set => SetValue((int)PropertyIndex.EdgePosition, value); }
        public float BandWidth { set => SetValue((int)PropertyIndex.BandWidth, value); }
        public float Softness { set => SetValue((int)PropertyIndex.Softness, value); }
        public int Mode { set => SetValue((int)PropertyIndex.Mode, value); }
        public float IsInverted { set => SetValue((int)PropertyIndex.IsInverted, value); }
        public float Rotation { set => SetValue((int)PropertyIndex.Rotation, value); }
        internal void ClearInput() => SetInput(0, null, true);

        public WaveClippingCustomEffect(IGraphicsDevicesAndContext devices)
            : base(Create<EffectImpl>(devices)) { }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InputLeft)]
            public float InputLeft { get => _cb.InputLeft; set { _cb.InputLeft = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InputTop)]
            public float InputTop { get => _cb.InputTop; set { _cb.InputTop = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InputWidth)]
            public float InputWidth { get => _cb.InputWidth; set { _cb.InputWidth = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InputHeight)]
            public float InputHeight { get => _cb.InputHeight; set { _cb.InputHeight = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amplitude)]
            public float Amplitude { get => _cb.Amplitude; set { _cb.Amplitude = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Frequency)]
            public float Frequency { get => _cb.Frequency; set { _cb.Frequency = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Phase)]
            public float Phase { get => _cb.Phase; set { _cb.Phase = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.EdgePosition)]
            public float EdgePosition { get => _cb.EdgePosition; set { _cb.EdgePosition = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BandWidth)]
            public float BandWidth { get => _cb.BandWidth; set { _cb.BandWidth = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Softness)]
            public float Softness { get => _cb.Softness; set { _cb.Softness = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Mode)]
            public int Mode { get => _cb.Mode; set { _cb.Mode = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.IsInverted)]
            public float IsInverted { get => _cb.IsInverted; set { _cb.IsInverted = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Rotation)]
            public float Rotation { get => _cb.Rotation; set { _cb.Rotation = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("WaveClipping")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(
                Vortice.RawRect[] inputRects,
                Vortice.RawRect[] inputOpaqueSubRects,
                out Vortice.RawRect outputRect,
                out Vortice.RawRect outputOpaqueSubRect)
            {
                base.MapInputRectsToOutputRect(inputRects, inputOpaqueSubRects, out outputRect, out outputOpaqueSubRect);

                if (inputRects.Length > 0)
                {
                    var r = inputRects[0];
                    _cb.InputLeft = r.Left;
                    _cb.InputTop = r.Top;
                    _cb.InputWidth = Math.Max(1.0f, r.Right - r.Left);
                    _cb.InputHeight = Math.Max(1.0f, r.Bottom - r.Top);
                    UpdateConstants();
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float Amplitude;
                public float Frequency;
                public float Phase;
                public float EdgePosition;
                public float BandWidth;
                public float Softness;
                public int Mode;
                public float IsInverted;
                public float Rotation;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }
        }
    }
}