using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    internal sealed class ColorTransferCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            DomainMinimum = 0,
            DomainScale,
            LightnessAmount,
            ColorAmount,
            TransferLut,
        }

        public Vector3 DomainMinimum { set => SetValue((int)PropertyIndex.DomainMinimum, value); }
        public Vector3 DomainScale { set => SetValue((int)PropertyIndex.DomainScale, value); }
        public float LightnessAmount { set => SetValue((int)PropertyIndex.LightnessAmount, value); }
        public float ColorAmount { set => SetValue((int)PropertyIndex.ColorAmount, value); }
        public byte[] TransferLut { set => SetValue((int)PropertyIndex.TransferLut, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private const int HeaderByteSize = 32;
            private const int ConstantBufferByteSize = HeaderByteSize + ColorTransferAnalyzer.LutByteSize;
            private const float MaximumDomainExtent = 16f;
            private const float MinimumDomainScale = 1e-3f;
            private const float MaximumDomainScale = 1e4f;

            private ConstantBuffer _cb;
            private readonly byte[] _lut = new byte[ColorTransferAnalyzer.LutByteSize];

            [CustomEffectProperty(PropertyType.Vector3, (int)PropertyIndex.DomainMinimum)]
            public Vector3 DomainMinimum { get => _cb.DomainMinimum; set { _cb.DomainMinimum = ClampVector(value, -MaximumDomainExtent, MaximumDomainExtent); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)PropertyIndex.DomainScale)]
            public Vector3 DomainScale { get => _cb.DomainScale; set { _cb.DomainScale = ClampVector(value, MinimumDomainScale, MaximumDomainScale); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightnessAmount)]
            public float LightnessAmount { get => _cb.LightnessAmount; set { _cb.LightnessAmount = Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorAmount)]
            public float ColorAmount { get => _cb.ColorAmount; set { _cb.ColorAmount = Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Blob, (int)PropertyIndex.TransferLut)]
            public byte[] TransferLut
            {
                get => _lut;
                set
                {
                    if (value is null)
                        return;
                    Array.Copy(value, _lut, Math.Min(value.Length, _lut.Length));
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ColorTransfer"))
            {
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in _cb);
                _lut.CopyTo(buffer[HeaderByteSize..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            private static Vector3 ClampVector(Vector3 value, float minimum, float maximum)
                => new(Clamp(value.X, minimum, maximum), Clamp(value.Y, minimum, maximum), Clamp(value.Z, minimum, maximum));

            private static float Clamp(float value, float minimum, float maximum)
                => float.IsNaN(value) ? minimum : Math.Clamp(value, minimum, maximum);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public Vector3 DomainMinimum;
                public float LightnessAmount;
                public Vector3 DomainScale;
                public float ColorAmount;
            }
        }
    }
}
