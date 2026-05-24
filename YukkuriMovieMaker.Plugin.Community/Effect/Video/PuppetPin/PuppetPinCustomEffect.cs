using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    internal sealed class PuppetPinCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const int MaxPins = 256;

        public float PinCount
        {
            set => SetValue((int)EffectImpl.Properties.PinCount, value);
            get => GetFloatValue((int)EffectImpl.Properties.PinCount);
        }

        public float Stiffness
        {
            set => SetValue((int)EffectImpl.Properties.Stiffness, value);
            get => GetFloatValue((int)EffectImpl.Properties.Stiffness);
        }

        public float MaxDisplacement
        {
            set => SetValue((int)EffectImpl.Properties.MaxDisplacement, value);
            get => GetFloatValue((int)EffectImpl.Properties.MaxDisplacement);
        }

        public float TightLocalLeft
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalLeft, value);
        }

        public float TightLocalTop
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalTop, value);
        }

        public float TightLocalRight
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalRight, value);
        }

        public float TightLocalBottom
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalBottom, value);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer _cb;
            float _tightLocalLeft, _tightLocalTop, _tightLocalRight, _tightLocalBottom;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.PinCount)]
            public float PinCount
            {
                get => _cb.PinCount;
                set { _cb.PinCount = System.Math.Clamp(value, 0f, MaxPins); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Stiffness)]
            public float Stiffness
            {
                get => _cb.Stiffness;
                set { _cb.Stiffness = System.Math.Clamp(value, 0.1f, 8f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.MaxDisplacement)]
            public float MaxDisplacement
            {
                get => _cb.MaxDisplacement;
                set { _cb.MaxDisplacement = System.Math.Max(value, 0f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalLeft)]
            public float TightLocalLeft { get => _tightLocalLeft; set => _tightLocalLeft = value; }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalTop)]
            public float TightLocalTop { get => _tightLocalTop; set => _tightLocalTop = value; }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalRight)]
            public float TightLocalRight { get => _tightLocalRight; set => _tightLocalRight = value; }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalBottom)]
            public float TightLocalBottom { get => _tightLocalBottom; set => _tightLocalBottom = value; }

            public EffectImpl() : base(ShaderResourceUri.Get("PuppetPin")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
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

                _cb.InputLeft = inputRect.Left;
                _cb.InputTop = inputRect.Top;
                _cb.InputWidth = inputRect.Right - inputRect.Left;
                _cb.InputHeight = inputRect.Bottom - inputRect.Top;
                UpdateConstants();

                bool hasTight = _tightLocalRight > _tightLocalLeft && _tightLocalBottom > _tightLocalTop;
                if (hasTight)
                {
                    float cx = inputRect.Left + _cb.InputWidth * 0.5f;
                    float cy = inputRect.Top + _cb.InputHeight * 0.5f;
                    int tl = (int)Math.Floor(cx + _tightLocalLeft);
                    int tt = (int)Math.Floor(cy + _tightLocalTop);
                    int tr = (int)Math.Ceiling(cx + _tightLocalRight);
                    int tb = (int)Math.Ceiling(cy + _tightLocalBottom);

                    if (tr > tl && tb > tt)
                    {
                        outputRect = new RawRect(tl, tt, tr, tb);
                    }
                    else
                    {
                        outputRect = inputRect;
                    }
                }
                else
                {
                    outputRect = inputRect;
                }

                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                    inputRects[0] = inputRect;
                if (inputRects.Length > 1)
                    inputRects[1] = new RawRect(0, 0, MaxPins, 1);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float PinCount;
                public float Stiffness;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float MaxDisplacement;
                public float Pad0;
            }

            public enum Properties : int
            {
                PinCount = 0,
                Stiffness = 1,
                MaxDisplacement = 2,
                TightLocalLeft = 3,
                TightLocalTop = 4,
                TightLocalRight = 5,
                TightLocalBottom = 6,
            }
        }
    }
}
