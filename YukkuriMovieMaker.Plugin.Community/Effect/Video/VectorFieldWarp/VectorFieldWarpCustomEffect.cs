using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldWarpCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const int MaxPoints = 32;
        public const int MaxIntegrationSteps = 16;
        public const float MaxDisplacementLimit = 2048f;

        public int PointCount { get => GetIntValue((int)EffectImpl.Properties.PointCount); set => SetValue((int)EffectImpl.Properties.PointCount, value); }
        public int IntegrationSteps { get => GetIntValue((int)EffectImpl.Properties.IntegrationSteps); set => SetValue((int)EffectImpl.Properties.IntegrationSteps, value); }
        public float Amount { get => GetFloatValue((int)EffectImpl.Properties.Amount); set => SetValue((int)EffectImpl.Properties.Amount, value); }
        public float MaxDisplacement { get => GetFloatValue((int)EffectImpl.Properties.MaxDisplacement); set => SetValue((int)EffectImpl.Properties.MaxDisplacement, value); }
        public byte[] PointData { set => SetValue((int)EffectImpl.Properties.PointData, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const int HeaderByteSize = 32;
            const int PointByteSize = 32;
            const int ConstantBufferByteSize = HeaderByteSize + MaxPoints * PointByteSize;

            ConstantBuffer constants;
            readonly byte[] pointData = new byte[MaxPoints * PointByteSize];

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.PointCount)]
            public int PointCount
            {
                get => constants.PointCount;
                set
                {
                    constants.PointCount = Math.Clamp(value, 0, MaxPoints);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.IntegrationSteps)]
            public int IntegrationSteps
            {
                get => constants.IntegrationSteps;
                set
                {
                    constants.IntegrationSteps = Math.Clamp(value, 1, MaxIntegrationSteps);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Amount)]
            public float Amount
            {
                get => constants.Amount;
                set
                {
                    constants.Amount = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.MaxDisplacement)]
            public float MaxDisplacement
            {
                get => constants.MaxDisplacement;
                set
                {
                    constants.MaxDisplacement = float.IsFinite(value) ? Math.Clamp(value, 0f, MaxDisplacementLimit) : 0f;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Blob, (int)Properties.PointData)]
            public byte[] PointData
            {
                get => pointData;
                set
                {
                    Array.Clear(pointData);
                    if (value is not null)
                        Array.Copy(value, pointData, Math.Min(value.Length, pointData.Length));
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("VectorFieldWarp"))
            {
                constants.IntegrationSteps = 8;
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in constants);
                pointData.CopyTo(buffer[HeaderByteSize..]);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                constants.InputLeft = inputRect.Left;
                constants.InputTop = inputRect.Top;
                constants.InputWidth = Math.Max(0, inputRect.Right - inputRect.Left);
                constants.InputHeight = Math.Max(0, inputRect.Bottom - inputRect.Top);
                UpdateConstants();

                outputRect = Inflate(inputRect, GetMargin());
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                    inputRects[0] = ClampInputRect(Inflate(outputRect, GetMargin() + 1));
            }

            int GetMargin()
            {
                if (constants.PointCount <= 0 || constants.Amount <= 0f || constants.MaxDisplacement <= 0f)
                    return 0;
                return (int)Math.Ceiling(constants.Amount * constants.MaxDisplacement);
            }

            static RawRect Inflate(RawRect rect, int margin)
            {
                if (margin <= 0)
                    return rect;

                return new RawRect(
                    Saturate((long)rect.Left - margin),
                    Saturate((long)rect.Top - margin),
                    Saturate((long)rect.Right + margin),
                    Saturate((long)rect.Bottom + margin));
            }

            static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public int PointCount;
                public int IntegrationSteps;
                public float Amount;
                public float MaxDisplacement;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
            }

            public enum Properties
            {
                PointCount,
                IntegrationSteps,
                Amount,
                MaxDisplacement,
                PointData,
            }
        }
    }
}
