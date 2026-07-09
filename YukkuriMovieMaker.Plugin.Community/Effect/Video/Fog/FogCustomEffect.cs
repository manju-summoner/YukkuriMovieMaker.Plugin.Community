using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    internal sealed class FogCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Density = 0,
            InvFeature,
            Time,
            Unevenness,
            FlowX,
            FlowY,
            BoilSpeed,
            Gradient,
            FogR,
            FogG,
            FogB,
            Seed,
            SunIntensity,
            SunAngle,
            SunR,
            SunG,
            SunB,
            DepthAmount,
            VpX,
            VpY,
            HazeMix,
        }

        public float Density { set => SetValue((int)PropertyIndex.Density, value); }
        public float InvFeature { set => SetValue((int)PropertyIndex.InvFeature, value); }
        public float Time { set => SetValue((int)PropertyIndex.Time, value); }
        public float Unevenness { set => SetValue((int)PropertyIndex.Unevenness, value); }
        public float FlowX { set => SetValue((int)PropertyIndex.FlowX, value); }
        public float FlowY { set => SetValue((int)PropertyIndex.FlowY, value); }
        public float BoilSpeed { set => SetValue((int)PropertyIndex.BoilSpeed, value); }
        public float Gradient { set => SetValue((int)PropertyIndex.Gradient, value); }
        public float FogR { set => SetValue((int)PropertyIndex.FogR, value); }
        public float FogG { set => SetValue((int)PropertyIndex.FogG, value); }
        public float FogB { set => SetValue((int)PropertyIndex.FogB, value); }
        public float Seed { set => SetValue((int)PropertyIndex.Seed, value); }
        public float SunIntensity { set => SetValue((int)PropertyIndex.SunIntensity, value); }
        public float SunAngle { set => SetValue((int)PropertyIndex.SunAngle, value); }
        public float SunR { set => SetValue((int)PropertyIndex.SunR, value); }
        public float SunG { set => SetValue((int)PropertyIndex.SunG, value); }
        public float SunB { set => SetValue((int)PropertyIndex.SunB, value); }
        public float DepthAmount { set => SetValue((int)PropertyIndex.DepthAmount, value); }
        public float VpX { set => SetValue((int)PropertyIndex.VpX, value); }
        public float VpY { set => SetValue((int)PropertyIndex.VpY, value); }
        public float HazeMix { set => SetValue((int)PropertyIndex.HazeMix, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Density)]
            public float Density { get => _cb.Density; set { _cb.Density = Math.Clamp(value, 0f, 10f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InvFeature)]
            public float InvFeature { get => _cb.InvFeature; set { _cb.InvFeature = Math.Clamp(value, 1e-5f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Time)]
            public float Time { get => _cb.Time; set { _cb.Time = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Unevenness)]
            public float Unevenness { get => _cb.Unevenness; set { _cb.Unevenness = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FlowX)]
            public float FlowX { get => _cb.FlowX; set { _cb.FlowX = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FlowY)]
            public float FlowY { get => _cb.FlowY; set { _cb.FlowY = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BoilSpeed)]
            public float BoilSpeed { get => _cb.BoilSpeed; set { _cb.BoilSpeed = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Gradient)]
            public float Gradient { get => _cb.Gradient; set { _cb.Gradient = Math.Clamp(value, -1f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FogR)]
            public float FogR { get => _cb.FogR; set { _cb.FogR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FogG)]
            public float FogG { get => _cb.FogG; set { _cb.FogG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FogB)]
            public float FogB { get => _cb.FogB; set { _cb.FogB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Seed)]
            public float Seed { get => _cb.Seed; set { _cb.Seed = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.SunIntensity)]
            public float SunIntensity { get => _cb.SunIntensity; set { _cb.SunIntensity = Math.Clamp(value, 0f, 10f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.SunAngle)]
            public float SunAngle { get => _cb.SunAngle; set { _cb.SunAngle = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.SunR)]
            public float SunR { get => _cb.SunR; set { _cb.SunR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.SunG)]
            public float SunG { get => _cb.SunG; set { _cb.SunG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.SunB)]
            public float SunB { get => _cb.SunB; set { _cb.SunB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.DepthAmount)]
            public float DepthAmount { get => _cb.DepthAmount; set { _cb.DepthAmount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.VpX)]
            public float VpX { get => _cb.VpX; set { _cb.VpX = Math.Clamp(value, -65536f, 65536f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.VpY)]
            public float VpY { get => _cb.VpY; set { _cb.VpY = Math.Clamp(value, -65536f, 65536f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.HazeMix)]
            public float HazeMix { get => _cb.HazeMix; set { _cb.HazeMix = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("Fog"))
            {
                _cb.InvFeature = 1f / 150f;
                _cb.FogR = 0.86f;
                _cb.FogG = 0.88f;
                _cb.FogB = 0.9f;
                _cb.SunR = 1f;
                _cb.SunG = 0.95f;
                _cb.SunB = 0.85f;
                _cb.HazeMix = 0.3f;
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length != 1)
                    throw new ArgumentException("InputRects must be length of 1", nameof(inputRects));

                var rect = ClampInputRect(inputRects[0]);
                _cb.InputTop = rect.Top;
                _cb.InputHeight = Math.Max(rect.Bottom - rect.Top, 1);
                _cb.InputLeft = rect.Left;
                _cb.InputWidth = Math.Max(rect.Right - rect.Left, 1);
                UpdateConstants();

                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                const int margin = 3;
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - margin),
                    Saturate((long)outputRect.Top - margin),
                    Saturate((long)outputRect.Right + margin),
                    Saturate((long)outputRect.Bottom + margin));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Density;
                public float InvFeature;
                public float Time;
                public float Unevenness;
                public float FlowX;
                public float FlowY;
                public float BoilSpeed;
                public float Gradient;
                public float FogR;
                public float FogG;
                public float FogB;
                public float Seed;
                public float SunIntensity;
                public float SunAngle;
                public float InputTop;
                public float InputHeight;
                public float SunR;
                public float SunG;
                public float SunB;
                public float DepthAmount;
                public float VpX;
                public float VpY;
                public float HazeMix;
                public float InputLeft;
                public float InputWidth;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }
        }
    }
}
