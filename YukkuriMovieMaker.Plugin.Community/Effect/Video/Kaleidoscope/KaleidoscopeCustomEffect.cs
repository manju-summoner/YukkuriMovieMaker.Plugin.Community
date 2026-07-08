using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Kaleidoscope
{
    public sealed class KaleidoscopeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Segments = 0,
            Rotation,
            Zoom,
            CenterX,
            CenterY,
            Mirror,
            Amount,
        }

        public float Segments { set => SetValue((int)PropertyIndex.Segments, value); }
        public float Rotation { set => SetValue((int)PropertyIndex.Rotation, value); }
        public float Zoom { set => SetValue((int)PropertyIndex.Zoom, value); }
        public float CenterX { set => SetValue((int)PropertyIndex.CenterX, value); }
        public float CenterY { set => SetValue((int)PropertyIndex.CenterY, value); }
        public float Mirror { set => SetValue((int)PropertyIndex.Mirror, value); }
        public float Amount { set => SetValue((int)PropertyIndex.Amount, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Segments)]
            public float Segments { get => _cb.Segments; set { _cb.Segments = Math.Clamp(value, 1f, 256f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Rotation)]
            public float Rotation { get => _cb.Rotation; set { _cb.Rotation = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Zoom)]
            public float Zoom { get => _cb.Zoom; set { _cb.Zoom = Math.Clamp(value, 1e-3f, 1e4f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.CenterX)]
            public float CenterX { get => _cb.CenterX; set { _cb.CenterX = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.CenterY)]
            public float CenterY { get => _cb.CenterY; set { _cb.CenterY = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Mirror)]
            public float Mirror { get => _cb.Mirror; set { _cb.Mirror = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amount)]
            public float Amount { get => _cb.Amount; set { _cb.Amount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("Kaleidoscope")) { }

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

                inputRect = ClampInputRect(inputRect);
                _cb.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
                UpdateConstants();
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length == 0)
                    return;

                inputRects[0] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public Vector4 InputBounds;
                public float Segments;
                public float Rotation;
                public float Zoom;
                public float CenterX;
                public float CenterY;
                public float Mirror;
                public float Amount;
                public float Pad0;
            }
        }
    }
}
