using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
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

        public byte[] PinData
        {
            set => SetValue((int)EffectImpl.Properties.PinData, value);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const float MaxLocalExtent = 4096f;
            const int HeaderByteSize = 32;
            const int ConstantBufferByteSize = HeaderByteSize + MaxPins * 16;

            ConstantBuffer _cb;
            readonly byte[] _pinData = new byte[MaxPins * 16];
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

            [CustomEffectProperty(PropertyType.Blob, (int)Properties.PinData)]
            public byte[] PinData
            {
                get => _pinData;
                set
                {
                    if (value is null)
                        return;
                    var length = Math.Min(value.Length, _pinData.Length);
                    Array.Copy(value, _pinData, length);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PuppetDeformation"))
            {
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                //ヘッダ(_cb 32byte) + ピンデータ(_pinData) を結合して定数バッファとして渡す。
                //ピンデータを入力テクスチャ(float32)ではなく定数バッファで渡すことで、
                //Direct2Dの中間テクスチャ精度がfloat32に昇格するのを防ぎ、
                //タイル分割境界のアーティファクトを回避する。
                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in _cb);
                _pinData.CopyTo(buffer[HeaderByteSize..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
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
                    inputRects[0] = new RawRect(inputRect.Left-2, inputRect.Top-2, inputRect.Right+2, inputRect.Bottom+2);
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
                public float Pad0;
                public float Pad1;
            }

            public enum Properties : int
            {
                PinCount = 0,
                Stiffness = 1,
                TightLocalLeft = 2,
                TightLocalTop = 3,
                TightLocalRight = 4,
                TightLocalBottom = 5,
                PinData = 6,
            }
        }
    }
}
