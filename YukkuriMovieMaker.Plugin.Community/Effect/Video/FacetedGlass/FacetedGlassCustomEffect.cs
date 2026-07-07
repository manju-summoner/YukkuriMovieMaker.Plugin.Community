using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FacetedGlass
{
    public sealed class FacetedGlassCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Amount = 0,
            CellSize,
            Irregularity,
            Relief,
            Rotation,
            Evolution,
            Refraction,
            RefractiveIndex,
            Dispersion,
            Reflection,
            Glint,
            BorderWidth,
            LightAngle,
            LightElevation,
            Seed,
        }

        public float Amount { set => SetValue((int)PropertyIndex.Amount, value); }
        public float CellSize { set => SetValue((int)PropertyIndex.CellSize, value); }
        public float Irregularity { set => SetValue((int)PropertyIndex.Irregularity, value); }
        public float Relief { set => SetValue((int)PropertyIndex.Relief, value); }
        public float Rotation { set => SetValue((int)PropertyIndex.Rotation, value); }
        public float Evolution { set => SetValue((int)PropertyIndex.Evolution, value); }
        public float Refraction { set => SetValue((int)PropertyIndex.Refraction, value); }
        public float RefractiveIndex { set => SetValue((int)PropertyIndex.RefractiveIndex, value); }
        public float Dispersion { set => SetValue((int)PropertyIndex.Dispersion, value); }
        public float Reflection { set => SetValue((int)PropertyIndex.Reflection, value); }
        public float Glint { set => SetValue((int)PropertyIndex.Glint, value); }
        public float BorderWidth { set => SetValue((int)PropertyIndex.BorderWidth, value); }
        public float LightAngle { set => SetValue((int)PropertyIndex.LightAngle, value); }
        public float LightElevation { set => SetValue((int)PropertyIndex.LightElevation, value); }
        public int Seed { set => SetValue((int)PropertyIndex.Seed, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amount)]
            public float Amount { get => _cb.Amount; set { _cb.Amount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.CellSize)]
            public float CellSize { get => _cb.CellSize; set { _cb.CellSize = Math.Max(value, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Irregularity)]
            public float Irregularity { get => _cb.Irregularity; set { _cb.Irregularity = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Relief)]
            public float Relief { get => _cb.Relief; set { _cb.Relief = Math.Clamp(value, 0f, 2f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Rotation)]
            public float Rotation { get => _cb.Rotation; set { _cb.Rotation = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Evolution)]
            public float Evolution { get => _cb.Evolution; set { _cb.Evolution = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Refraction)]
            public float Refraction { get => _cb.Refraction; set { _cb.Refraction = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.RefractiveIndex)]
            public float RefractiveIndex { get => _cb.RefractiveIndex; set { _cb.RefractiveIndex = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Dispersion)]
            public float Dispersion { get => _cb.Dispersion; set { _cb.Dispersion = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Reflection)]
            public float Reflection { get => _cb.Reflection; set { _cb.Reflection = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Glint)]
            public float Glint { get => _cb.Glint; set { _cb.Glint = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.BorderWidth)]
            public float BorderWidth { get => _cb.BorderWidth; set { _cb.BorderWidth = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightAngle)]
            public float LightAngle { get => _cb.LightAngle; set { _cb.LightAngle = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightElevation)]
            public float LightElevation { get => _cb.LightElevation; set { _cb.LightElevation = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Seed)]
            public int Seed { get => _cb.Seed; set { _cb.Seed = value; UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("FacetedGlass")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(
                RawRect[] inputRects,
                RawRect[] inputOpaqueSubRects,
                out RawRect outputRect,
                out RawRect outputOpaqueSubRect)
            {
                base.MapInputRectsToOutputRect(inputRects, inputOpaqueSubRects, out outputRect, out outputOpaqueSubRect);

                if (inputRects.Length > 0)
                {
                    var r = inputRects[0];
                    _cb.InputBounds = new Vector4(r.Left, r.Top, r.Right, r.Bottom);
                    UpdateConstants();
                }
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length == 0)
                    return;

                var margin = (int)Math.Ceiling(_cb.Refraction * 1.25f + 2f);
                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - margin),
                    Saturate((long)outputRect.Top - margin),
                    Saturate((long)outputRect.Right + margin),
                    Saturate((long)outputRect.Bottom + margin));
            }

            private static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public Vector4 InputBounds;
                public float Amount;
                public float CellSize;
                public float Irregularity;
                public float Relief;
                public float Rotation;
                public float Evolution;
                public float Refraction;
                public float RefractiveIndex;
                public float Dispersion;
                public float Reflection;
                public float Glint;
                public float BorderWidth;
                public float LightAngle;
                public float LightElevation;
                public int Seed;
                public float Pad0;
            }
        }
    }
}
