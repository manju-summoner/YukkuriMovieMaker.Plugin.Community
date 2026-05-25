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
        public float TightLocalLeft
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalLeft, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalLeft);
        }
        public float TightLocalTop
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalTop, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalTop);
        }
        public float TightLocalRight
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalRight, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalRight);
        }
        public float TightLocalBottom
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalBottom, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalBottom);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const float MaxLocalExtent = 4096f;

            ConstantBuffer _cb;
            float _tightLocalLeft, _tightLocalTop, _tightLocalRight, _tightLocalBottom;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.PinCount)]
            public float PinCount { get => _cb.PinCount; set { _cb.PinCount = Math.Clamp(value, 0f, MaxPins); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Stiffness)]
            public float Stiffness { get => _cb.Stiffness; set { _cb.Stiffness = Math.Clamp(value, 0.1f, 8f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalLeft)]
            public float TightLocalLeft { get => _tightLocalLeft; set => _tightLocalLeft = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalTop)]
            public float TightLocalTop { get => _tightLocalTop; set => _tightLocalTop = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalRight)]
            public float TightLocalRight { get => _tightLocalRight; set => _tightLocalRight = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalBottom)]
            public float TightLocalBottom { get => _tightLocalBottom; set => _tightLocalBottom = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            public EffectImpl() : base(ShaderResourceUri.Get("PuppetPin"))
            {
            }

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

                if (_tightLocalRight > _tightLocalLeft && _tightLocalBottom > _tightLocalTop)
                {
                    float cx = inputRect.Left + _cb.InputWidth * 0.5f;
                    float cy = inputRect.Top + _cb.InputHeight * 0.5f;
                    int tl = (int)Math.Floor(cx + _tightLocalLeft);
                    int tt = (int)Math.Floor(cy + _tightLocalTop);
                    int tr = (int)Math.Ceiling(cx + _tightLocalRight);
                    int tb = (int)Math.Ceiling(cy + _tightLocalBottom);

                    outputRect = tr > tl && tb > tt
                        ? new RawRect(tl, tt, tr, tb)
                        : inputRect;
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
            }

            public enum Properties : int
            {
                PinCount = 0,
                Stiffness = 1,
                TightLocalLeft = 2,
                TightLocalTop = 3,
                TightLocalRight = 4,
                TightLocalBottom = 5,
            }
        }
    }
}
