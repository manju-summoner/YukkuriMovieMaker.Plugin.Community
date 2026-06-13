using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PartialOutline
{
    internal sealed class PartialOutlineCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }

        public float BandCenter
        {
            set => SetValue((int)EffectImpl.Properties.BandCenter, value);
            get => GetFloatValue((int)EffectImpl.Properties.BandCenter);
        }

        public float HalfBandWidth
        {
            set => SetValue((int)EffectImpl.Properties.HalfBandWidth, value);
            get => GetFloatValue((int)EffectImpl.Properties.HalfBandWidth);
        }

        public float Softness
        {
            set => SetValue((int)EffectImpl.Properties.Softness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Softness);
        }

        public float CenterX
        {
            set => SetValue((int)EffectImpl.Properties.CenterX, value);
            get => GetFloatValue((int)EffectImpl.Properties.CenterX);
        }

        public float CenterY
        {
            set => SetValue((int)EffectImpl.Properties.CenterY, value);
            get => GetFloatValue((int)EffectImpl.Properties.CenterY);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Angle
            {
                get => _cb.Angle;
                set { _cb.Angle = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.BandCenter)]
            public float BandCenter
            {
                get => _cb.BandCenter;
                set { _cb.BandCenter = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.HalfBandWidth)]
            public float HalfBandWidth
            {
                get => _cb.HalfBandWidth;
                set { _cb.HalfBandWidth = Math.Max(value, 0f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Softness)]
            public float Softness
            {
                get => _cb.Softness;
                set { _cb.Softness = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.CenterX)]
            public float CenterX
            {
                get => _cb.CenterX;
                set { _cb.CenterX = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.CenterY)]
            public float CenterY
            {
                get => _cb.CenterY;
                set { _cb.CenterY = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PartialOutline"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects.Length > 0 ? inputRects[0] : default;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                    inputRects[0] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Angle;
                public float BandCenter;
                public float HalfBandWidth;
                public float Softness;
                public float CenterX;
                public float CenterY;
                public float Pad0;
                public float Pad1;
            }

            public enum Properties : int
            {
                Angle = 0,
                BandCenter = 1,
                HalfBandWidth = 2,
                Softness = 3,
                CenterX = 4,
                CenterY = 5,
            }
        }
    }
}
