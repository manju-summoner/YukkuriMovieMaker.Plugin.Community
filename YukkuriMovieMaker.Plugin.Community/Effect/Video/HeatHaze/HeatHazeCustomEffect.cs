using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    internal sealed class HeatHazeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Strength
        {
            set => SetValue((int)EffectImpl.Properties.Strength, value);
            get => GetFloatValue((int)EffectImpl.Properties.Strength);
        }
        public float NoiseScale
        {
            set => SetValue((int)EffectImpl.Properties.NoiseScale, value);
            get => GetFloatValue((int)EffectImpl.Properties.NoiseScale);
        }
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        public float FlowSpeed
        {
            set => SetValue((int)EffectImpl.Properties.FlowSpeed, value);
            get => GetFloatValue((int)EffectImpl.Properties.FlowSpeed);
        }
        public float BoilSpeed
        {
            set => SetValue((int)EffectImpl.Properties.BoilSpeed, value);
            get => GetFloatValue((int)EffectImpl.Properties.BoilSpeed);
        }
        public float ChromaticAberration
        {
            set => SetValue((int)EffectImpl.Properties.ChromaticAberration, value);
            get => GetFloatValue((int)EffectImpl.Properties.ChromaticAberration);
        }
        public int EnableBlur
        {
            set => SetValue((int)EffectImpl.Properties.EnableBlur, value);
            get => GetIntValue((int)EffectImpl.Properties.EnableBlur);
        }
        public float BlurStrength
        {
            set => SetValue((int)EffectImpl.Properties.BlurStrength, value);
            get => GetFloatValue((int)EffectImpl.Properties.BlurStrength);
        }
        public float Time
        {
            set => SetValue((int)EffectImpl.Properties.Time, value);
            get => GetFloatValue((int)EffectImpl.Properties.Time);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.NoiseScale)]
            public float NoiseScale { get => _cb.NoiseScale; set { _cb.NoiseScale = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Angle { get => _cb.Angle; set { _cb.Angle = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.FlowSpeed)]
            public float FlowSpeed { get => _cb.FlowSpeed; set { _cb.FlowSpeed = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.BoilSpeed)]
            public float BoilSpeed { get => _cb.BoilSpeed; set { _cb.BoilSpeed = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ChromaticAberration)]
            public float ChromaticAberration { get => _cb.ChromaticAberrationPx; set { _cb.ChromaticAberrationPx = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.EnableBlur)]
            public int EnableBlur { get => _cb.EnableBlur; set { _cb.EnableBlur = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.BlurStrength)]
            public float BlurStrength { get => _cb.BlurStrengthPx; set { _cb.BlurStrengthPx = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Time)]
            public float Time { get => _cb.Time; set { _cb.Time = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("HeatHaze"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);

                int w = Math.Max(1, inputRect.Right - inputRect.Left);
                int h = Math.Max(1, inputRect.Bottom - inputRect.Top);

                float maxDispUV = _cb.Strength * 0.1f;
                int dispPx = (int)Math.Ceiling(Math.Max(maxDispUV * w, maxDispUV * h));
                int aberPx = (int)Math.Ceiling(_cb.ChromaticAberrationPx);
                int blurPx = _cb.EnableBlur != 0 ? (int)Math.Ceiling(_cb.BlurStrengthPx * 2f) : 0;

                int padding = Math.Min(dispPx + aberPx + blurPx + 2, 4096);

                outputRect = new RawRect(
                    inputRect.Left - padding,
                    inputRect.Top - padding,
                    inputRect.Right + padding,
                    inputRect.Bottom + padding);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Strength;
                public float NoiseScale;
                public float Angle;
                public float FlowSpeed;
                public float BoilSpeed;
                public float ChromaticAberrationPx;
                public int EnableBlur;
                public float BlurStrengthPx;
                public float Time;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }

            public enum Properties : int
            {
                Strength = 0,
                NoiseScale = 1,
                Angle = 2,
                FlowSpeed = 3,
                BoilSpeed = 4,
                ChromaticAberration = 5,
                EnableBlur = 6,
                BlurStrength = 7,
                Time = 8,
            }
        }
    }
}
