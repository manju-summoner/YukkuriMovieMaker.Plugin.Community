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
            ItemToSceneX,
            ItemToSceneY,
            ItemToSceneW,
            SceneToGrid,
            PositionAmount,
            TransferLut,
            LocalDelta,
        }

        public Vector3 DomainMinimum { set => SetValue((int)PropertyIndex.DomainMinimum, value); }
        public Vector3 DomainScale { set => SetValue((int)PropertyIndex.DomainScale, value); }
        public float LightnessAmount { set => SetValue((int)PropertyIndex.LightnessAmount, value); }
        public float ColorAmount { set => SetValue((int)PropertyIndex.ColorAmount, value); }
        public Vector4 ItemToSceneX { set => SetValue((int)PropertyIndex.ItemToSceneX, value); }
        public Vector4 ItemToSceneY { set => SetValue((int)PropertyIndex.ItemToSceneY, value); }
        public Vector4 ItemToSceneW { set => SetValue((int)PropertyIndex.ItemToSceneW, value); }
        public Vector4 SceneToGrid { set => SetValue((int)PropertyIndex.SceneToGrid, value); }
        public float PositionAmount { set => SetValue((int)PropertyIndex.PositionAmount, value); }
        public byte[] TransferLut { set => SetValue((int)PropertyIndex.TransferLut, value); }
        public byte[] LocalDelta { set => SetValue((int)PropertyIndex.LocalDelta, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private const int HeaderByteSize = 112;
            private const int ConstantBufferByteSize = HeaderByteSize + ColorTransferAnalyzer.LutByteSize + ColorTransferAnalyzer.LocalDeltaByteSize;
            private const float MaximumDomainExtent = 16f;
            private const float MinimumDomainScale = 1e-3f;
            private const float MaximumDomainScale = 1e4f;
            private const float MaximumMappingExtent = 1e6f;

            private ConstantBuffer _cb;
            private readonly byte[] _lut = new byte[ColorTransferAnalyzer.LutByteSize];
            private readonly byte[] _localDelta = new byte[ColorTransferAnalyzer.LocalDeltaByteSize];

            [CustomEffectProperty(PropertyType.Vector3, (int)PropertyIndex.DomainMinimum)]
            public Vector3 DomainMinimum { get => _cb.DomainMinimum; set { _cb.DomainMinimum = ClampVector(value, -MaximumDomainExtent, MaximumDomainExtent); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)PropertyIndex.DomainScale)]
            public Vector3 DomainScale { get => _cb.DomainScale; set { _cb.DomainScale = ClampVector(value, MinimumDomainScale, MaximumDomainScale); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightnessAmount)]
            public float LightnessAmount { get => _cb.LightnessAmount; set { _cb.LightnessAmount = Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ColorAmount)]
            public float ColorAmount { get => _cb.ColorAmount; set { _cb.ColorAmount = Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)PropertyIndex.ItemToSceneX)]
            public Vector4 ItemToSceneX { get => _cb.ItemToSceneX; set { _cb.ItemToSceneX = ClampMapping(value); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)PropertyIndex.ItemToSceneY)]
            public Vector4 ItemToSceneY { get => _cb.ItemToSceneY; set { _cb.ItemToSceneY = ClampMapping(value); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)PropertyIndex.ItemToSceneW)]
            public Vector4 ItemToSceneW { get => _cb.ItemToSceneW; set { _cb.ItemToSceneW = ClampMapping(value); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)PropertyIndex.SceneToGrid)]
            public Vector4 SceneToGrid { get => _cb.SceneToGrid; set { _cb.SceneToGrid = ClampMapping(value); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.PositionAmount)]
            public float PositionAmount { get => _cb.PositionAmount; set { _cb.PositionAmount = Clamp(value, 0f, 1f); UpdateConstants(); } }

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

            [CustomEffectProperty(PropertyType.Blob, (int)PropertyIndex.LocalDelta)]
            public byte[] LocalDelta
            {
                get => _localDelta;
                set
                {
                    if (value is null)
                        return;
                    Array.Copy(value, _localDelta, Math.Min(value.Length, _localDelta.Length));
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
                _localDelta.CopyTo(buffer[(HeaderByteSize + ColorTransferAnalyzer.LutByteSize)..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            private static Vector4 ClampMapping(Vector4 value)
                => new(
                    Clamp(value.X, -MaximumMappingExtent, MaximumMappingExtent),
                    Clamp(value.Y, -MaximumMappingExtent, MaximumMappingExtent),
                    Clamp(value.Z, -MaximumMappingExtent, MaximumMappingExtent),
                    Clamp(value.W, -MaximumMappingExtent, MaximumMappingExtent));

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
                public Vector4 ItemToSceneX;
                public Vector4 ItemToSceneY;
                public Vector4 ItemToSceneW;
                public Vector4 SceneToGrid;
                public float PositionAmount;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }
        }
    }
}
