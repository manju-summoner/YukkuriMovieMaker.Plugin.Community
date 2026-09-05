using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.MetaballSpectrum
{
    internal sealed class MetaballSpectrumCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const int MaxValues = 256;
        public const int MaxWindow = 64;
        public const int ValueByteSize = MaxValues * sizeof(float);

        private enum PropertyIndex
        {
            FieldWidth = 0,
            FieldHeight,
            ValueCount,
            BlobRadius,
            Threshold,
            Window,
            Bipolar,
            ColorR,
            ColorG,
            ColorB,
            ColorA,
            Values,
        }

        public float FieldWidth { set => SetValue((int)PropertyIndex.FieldWidth, value); }
        public float FieldHeight { set => SetValue((int)PropertyIndex.FieldHeight, value); }
        public float ValueCount { set => SetValue((int)PropertyIndex.ValueCount, value); }
        public float BlobRadius { set => SetValue((int)PropertyIndex.BlobRadius, value); }
        public float Threshold { set => SetValue((int)PropertyIndex.Threshold, value); }
        public float Window { set => SetValue((int)PropertyIndex.Window, value); }
        public float Bipolar { set => SetValue((int)PropertyIndex.Bipolar, value); }
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

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FieldWidth)]
            public float FieldWidth { get => _cb.FieldWidth; set { _cb.FieldWidth = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.FieldHeight)]
            public float FieldHeight { get => _cb.FieldHeight; set { _cb.FieldHeight = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ValueCount)]
            public float ValueCount { get => _cb.ValueCount; set { _cb.ValueCount = Math.Clamp(value, 0f, MaxValues); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BlobRadius)]
            public float BlobRadius { get => _cb.BlobRadius; set { _cb.BlobRadius = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Threshold)]
            public float Threshold { get => _cb.Threshold; set { _cb.Threshold = Math.Clamp(value, 0.001f, 0.999f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Window)]
            public float Window { get => _cb.Window; set { _cb.Window = Math.Clamp(value, 0f, MaxWindow); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Bipolar)]
            public float Bipolar { get => _cb.Bipolar; set { _cb.Bipolar = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

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

            public EffectImpl() : base(ShaderResourceUri.Get("MetaballSpectrum"))
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
                public float FieldWidth;
                public float FieldHeight;
                public float ValueCount;
                public float BlobRadius;
                public float Threshold;
                public float Window;
                public float Pad0;
                public float Pad1;
                public float Bipolar;
                public float Pad2;
                public float Pad3;
                public float Pad4;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
            }
        }
    }
}
