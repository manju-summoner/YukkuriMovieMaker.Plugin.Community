using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.StringSpectrum
{
    internal sealed class StringSpectrumCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const int MaxModes = 64;
        public const int ModeByteSize = MaxModes * sizeof(float);

        private enum PropertyIndex
        {
            Width = 0,
            Amplitude,
            ModeCount,
            Thickness,
            ColorR,
            ColorG,
            ColorB,
            ColorA,
            Modes,
        }

        public float Width { set => SetValue((int)PropertyIndex.Width, value); }
        public float Amplitude { set => SetValue((int)PropertyIndex.Amplitude, value); }
        public float ModeCount { set => SetValue((int)PropertyIndex.ModeCount, value); }
        public float Thickness { set => SetValue((int)PropertyIndex.Thickness, value); }
        public float ColorR { set => SetValue((int)PropertyIndex.ColorR, value); }
        public float ColorG { set => SetValue((int)PropertyIndex.ColorG, value); }
        public float ColorB { set => SetValue((int)PropertyIndex.ColorB, value); }
        public float ColorA { set => SetValue((int)PropertyIndex.ColorA, value); }
        public byte[] Modes { set => SetValue((int)PropertyIndex.Modes, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private const int HeaderByteSize = 32;
            private const int ConstantBufferByteSize = HeaderByteSize + ModeByteSize;

            private ConstantBuffer _cb;
            private readonly byte[] _modes = new byte[ModeByteSize];

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Width)]
            public float Width { get => _cb.Width; set { _cb.Width = Math.Max(value, 0.001f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amplitude)]
            public float Amplitude { get => _cb.Amplitude; set { _cb.Amplitude = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ModeCount)]
            public float ModeCount { get => _cb.ModeCount; set { _cb.ModeCount = Math.Clamp(value, 0f, MaxModes); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Thickness)]
            public float Thickness { get => _cb.Thickness; set { _cb.Thickness = Math.Max(value, 0.01f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorR)]
            public float ColorR { get => _cb.ColorR; set { _cb.ColorR = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorG)]
            public float ColorG { get => _cb.ColorG; set { _cb.ColorG = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorB)]
            public float ColorB { get => _cb.ColorB; set { _cb.ColorB = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorA)]
            public float ColorA { get => _cb.ColorA; set { _cb.ColorA = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Blob, (int)PropertyIndex.Modes)]
            public byte[] Modes
            {
                get => _modes;
                set
                {
                    if (value is null)
                        return;
                    var length = Math.Min(value.Length, _modes.Length);
                    Array.Copy(value, _modes, length);
                    Array.Clear(_modes, length, _modes.Length - length);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("StringSpectrum"))
            {
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in _cb);
                _modes.CopyTo(buffer[HeaderByteSize..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Width;
                public float Amplitude;
                public float ModeCount;
                public float Thickness;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
            }
        }
    }
}
