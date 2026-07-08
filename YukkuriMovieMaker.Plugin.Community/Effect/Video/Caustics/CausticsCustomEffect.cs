using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Caustics
{
    internal sealed class CausticsCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Displacement = 0,
            InvFeature,
            Time,
            Strength,
            Sigma,
            Dispersion,
            Focus,
            Seed,
            LightR,
            LightG,
            LightB,
            LightOnly,
            AbsorbR,
            AbsorbG,
            AbsorbB,
            Absorption,
            FlowX,
            FlowY,
            AnisoScale,
            AnisoAngle,
            BoilSpeed,
        }

        public float Displacement { set => SetValue((int)PropertyIndex.Displacement, value); }
        public float InvFeature { set => SetValue((int)PropertyIndex.InvFeature, value); }
        public float Time { set => SetValue((int)PropertyIndex.Time, value); }
        public float Strength { set => SetValue((int)PropertyIndex.Strength, value); }
        public float Sigma { set => SetValue((int)PropertyIndex.Sigma, value); }
        public float Dispersion { set => SetValue((int)PropertyIndex.Dispersion, value); }
        public float Focus { set => SetValue((int)PropertyIndex.Focus, value); }
        public float Seed { set => SetValue((int)PropertyIndex.Seed, value); }
        public float LightR { set => SetValue((int)PropertyIndex.LightR, value); }
        public float LightG { set => SetValue((int)PropertyIndex.LightG, value); }
        public float LightB { set => SetValue((int)PropertyIndex.LightB, value); }
        public int LightOnly { set => SetValue((int)PropertyIndex.LightOnly, value); }
        public float AbsorbR { set => SetValue((int)PropertyIndex.AbsorbR, value); }
        public float AbsorbG { set => SetValue((int)PropertyIndex.AbsorbG, value); }
        public float AbsorbB { set => SetValue((int)PropertyIndex.AbsorbB, value); }
        public float Absorption { set => SetValue((int)PropertyIndex.Absorption, value); }
        public float FlowX { set => SetValue((int)PropertyIndex.FlowX, value); }
        public float FlowY { set => SetValue((int)PropertyIndex.FlowY, value); }
        public float AnisoScale { set => SetValue((int)PropertyIndex.AnisoScale, value); }
        public float AnisoAngle { set => SetValue((int)PropertyIndex.AnisoAngle, value); }
        public float BoilSpeed { set => SetValue((int)PropertyIndex.BoilSpeed, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Displacement)]
            public float Displacement { get => _cb.Displacement; set { _cb.Displacement = Math.Clamp(value, 0f, 2000f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InvFeature)]
            public float InvFeature { get => _cb.InvFeature; set { _cb.InvFeature = Math.Clamp(value, 1e-5f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Time)]
            public float Time { get => _cb.Time; set { _cb.Time = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Clamp(value, 0f, 10f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Sigma)]
            public float Sigma { get => _cb.Sigma; set { _cb.Sigma = Math.Clamp(value, 1e-4f, 4f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Dispersion)]
            public float Dispersion { get => _cb.Dispersion; set { _cb.Dispersion = Math.Clamp(value, 0f, 0.5f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Focus)]
            public float Focus { get => _cb.Focus; set { _cb.Focus = Math.Clamp(value, 0f, 8f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Seed)]
            public float Seed { get => _cb.Seed; set { _cb.Seed = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightR)]
            public float LightR { get => _cb.LightR; set { _cb.LightR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightG)]
            public float LightG { get => _cb.LightG; set { _cb.LightG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightB)]
            public float LightB { get => _cb.LightB; set { _cb.LightB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.LightOnly)]
            public int LightOnly { get => _cb.LightOnly; set { _cb.LightOnly = value != 0 ? 1 : 0; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AbsorbR)]
            public float AbsorbR { get => _cb.AbsorbR; set { _cb.AbsorbR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AbsorbG)]
            public float AbsorbG { get => _cb.AbsorbG; set { _cb.AbsorbG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AbsorbB)]
            public float AbsorbB { get => _cb.AbsorbB; set { _cb.AbsorbB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Absorption)]
            public float Absorption { get => _cb.Absorption; set { _cb.Absorption = Math.Clamp(value, 0f, 4f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FlowX)]
            public float FlowX { get => _cb.FlowX; set { _cb.FlowX = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FlowY)]
            public float FlowY { get => _cb.FlowY; set { _cb.FlowY = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AnisoScale)]
            public float AnisoScale { get => _cb.AnisoScale; set { _cb.AnisoScale = Math.Clamp(value, 0.02f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.AnisoAngle)]
            public float AnisoAngle { get => _cb.AnisoAngle; set { _cb.AnisoAngle = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BoilSpeed)]
            public float BoilSpeed { get => _cb.BoilSpeed; set { _cb.BoilSpeed = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("Caustics"))
            {
                _cb.InvFeature = 1f / 150f;
                _cb.Sigma = 0.08f;
                _cb.Focus = 1f;
                _cb.AnisoScale = 1f;
                _cb.LightR = 1f;
                _cb.LightG = 1f;
                _cb.LightB = 1f;
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            private int Padding()
            {
                var margin = Math.Min(Math.Max(_cb.Displacement, 0f) * (1f + _cb.Dispersion) + 3f, 4096f);
                return (int)Math.Ceiling(margin);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                var padding = Padding();
                outputRect = new RawRect(
                    inputRect.Left - padding,
                    inputRect.Top - padding,
                    inputRect.Right + padding,
                    inputRect.Bottom + padding);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    inputRects[0] = inputRect;
                    return;
                }

                var padding = Padding();
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - padding),
                    Saturate((long)outputRect.Top - padding),
                    Saturate((long)outputRect.Right + padding),
                    Saturate((long)outputRect.Bottom + padding));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Displacement;
                public float InvFeature;
                public float Time;
                public float Strength;
                public float Sigma;
                public float Dispersion;
                public float Focus;
                public float Seed;
                public float LightR;
                public float LightG;
                public float LightB;
                public int LightOnly;
                public float AbsorbR;
                public float AbsorbG;
                public float AbsorbB;
                public float Absorption;
                public float FlowX;
                public float FlowY;
                public float AnisoScale;
                public float AnisoAngle;
                public float BoilSpeed;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }
        }
    }
}
