using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.RippleSpectrum
{
    internal sealed class RippleSpectrumCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const int MaxValues = 256;
        public const int MaxWindow = 64;
        public const int ValueByteSize = MaxValues * sizeof(float);

        private enum PropertyIndex
        {
            InnerRadius = 0,
            Reach,
            ValueCount,
            TravelOffset,
            MinThickness,
            MaxThickness,
            Window,
            Decay,
            ValueFollow,
            ColorR,
            ColorG,
            ColorB,
            ColorA,
            Values,
        }

        public float InnerRadius { set => SetValue((int)PropertyIndex.InnerRadius, value); }
        public float Reach { set => SetValue((int)PropertyIndex.Reach, value); }
        public float ValueCount { set => SetValue((int)PropertyIndex.ValueCount, value); }
        public float TravelOffset { set => SetValue((int)PropertyIndex.TravelOffset, value); }
        public float MinThickness { set => SetValue((int)PropertyIndex.MinThickness, value); }
        public float MaxThickness { set => SetValue((int)PropertyIndex.MaxThickness, value); }
        public float Window { set => SetValue((int)PropertyIndex.Window, value); }
        public float Decay { set => SetValue((int)PropertyIndex.Decay, value); }
        public float ValueFollow { set => SetValue((int)PropertyIndex.ValueFollow, value); }
        public float ColorR { set => SetValue((int)PropertyIndex.ColorR, value); }
        public float ColorG { set => SetValue((int)PropertyIndex.ColorG, value); }
        public float ColorB { set => SetValue((int)PropertyIndex.ColorB, value); }
        public float ColorA { set => SetValue((int)PropertyIndex.ColorA, value); }
        public byte[] Values { set => SetValue((int)PropertyIndex.Values, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private const int HeaderByteSize = 64;
            private const int ConstantBufferByteSize = HeaderByteSize + ValueByteSize;

            private ConstantBuffer _cb;
            private readonly byte[] _values = new byte[ValueByteSize];

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.InnerRadius)]
            public float InnerRadius { get => _cb.InnerRadius; set { _cb.InnerRadius = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Reach)]
            public float Reach { get => _cb.Reach; set { _cb.Reach = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ValueCount)]
            public float ValueCount { get => _cb.ValueCount; set { _cb.ValueCount = Math.Clamp(value, 0f, MaxValues); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.TravelOffset)]
            public float TravelOffset { get => _cb.TravelOffset; set { _cb.TravelOffset = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.MinThickness)]
            public float MinThickness { get => _cb.MinThickness; set { _cb.MinThickness = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.MaxThickness)]
            public float MaxThickness { get => _cb.MaxThickness; set { _cb.MaxThickness = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Window)]
            public float Window { get => _cb.Window; set { _cb.Window = Math.Clamp(value, 0f, MaxWindow); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Decay)]
            public float Decay { get => _cb.Decay; set { _cb.Decay = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ValueFollow)]
            public float ValueFollow { get => _cb.ValueFollow; set { _cb.ValueFollow = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorR)]
            public float ColorR { get => _cb.ColorR; set { _cb.ColorR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorG)]
            public float ColorG { get => _cb.ColorG; set { _cb.ColorG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorB)]
            public float ColorB { get => _cb.ColorB; set { _cb.ColorB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorA)]
            public float ColorA { get => _cb.ColorA; set { _cb.ColorA = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Blob, (int)PropertyIndex.Values)]
            public byte[] Values
            {
                get => _values;
                set
                {
                    if (value is null)
                        return;
                    var length = Math.Min(value.Length, _values.Length);
                    Array.Copy(value, _values, length);
                    Array.Clear(_values, length, _values.Length - length);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("RippleSpectrum"))
            {
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in _cb);
                _values.CopyTo(buffer[HeaderByteSize..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float InnerRadius;
                public float Reach;
                public float ValueCount;
                public float TravelOffset;
                public float MinThickness;
                public float MaxThickness;
                public float Window;
                public float Decay;
                public float ValueFollow;
                public float Pad0;
                public float Pad1;
                public float Pad2;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
            }
        }
    }
}
