using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Tritone;

internal sealed class TritoneCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    public float ShadowR
    {
        get => GetFloatValue((int)EffectImpl.Properties.ShadowR);
        set => SetValue((int)EffectImpl.Properties.ShadowR, value);
    }
    public float ShadowG
    {
        get => GetFloatValue((int)EffectImpl.Properties.ShadowG);
        set => SetValue((int)EffectImpl.Properties.ShadowG, value);
    }
    public float ShadowB
    {
        get => GetFloatValue((int)EffectImpl.Properties.ShadowB);
        set => SetValue((int)EffectImpl.Properties.ShadowB, value);
    }

    public float MidtoneR
    {
        get => GetFloatValue((int)EffectImpl.Properties.MidtoneR);
        set => SetValue((int)EffectImpl.Properties.MidtoneR, value);
    }
    public float MidtoneG
    {
        get => GetFloatValue((int)EffectImpl.Properties.MidtoneG);
        set => SetValue((int)EffectImpl.Properties.MidtoneG, value);
    }
    public float MidtoneB
    {
        get => GetFloatValue((int)EffectImpl.Properties.MidtoneB);
        set => SetValue((int)EffectImpl.Properties.MidtoneB, value);
    }

    public float HighlightR
    {
        get => GetFloatValue((int)EffectImpl.Properties.HighlightR);
        set => SetValue((int)EffectImpl.Properties.HighlightR, value);
    }
    public float HighlightG
    {
        get => GetFloatValue((int)EffectImpl.Properties.HighlightG);
        set => SetValue((int)EffectImpl.Properties.HighlightG, value);
    }
    public float HighlightB
    {
        get => GetFloatValue((int)EffectImpl.Properties.HighlightB);
        set => SetValue((int)EffectImpl.Properties.HighlightB, value);
    }

    public float MidPosition
    {
        get => GetFloatValue((int)EffectImpl.Properties.MidPosition);
        set => SetValue((int)EffectImpl.Properties.MidPosition, value);
    }

    [CustomEffect(1)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;

        [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowR)]
        public float ShadowR { get => _cb.ShadowR; set { _cb.ShadowR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowG)]
        public float ShadowG { get => _cb.ShadowG; set { _cb.ShadowG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowB)]
        public float ShadowB { get => _cb.ShadowB; set { _cb.ShadowB = value; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.MidtoneR)]
        public float MidtoneR { get => _cb.MidtoneR; set { _cb.MidtoneR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.MidtoneG)]
        public float MidtoneG { get => _cb.MidtoneG; set { _cb.MidtoneG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.MidtoneB)]
        public float MidtoneB { get => _cb.MidtoneB; set { _cb.MidtoneB = value; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.HighlightR)]
        public float HighlightR { get => _cb.HighlightR; set { _cb.HighlightR = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.HighlightG)]
        public float HighlightG { get => _cb.HighlightG; set { _cb.HighlightG = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, (int)Properties.HighlightB)]
        public float HighlightB { get => _cb.HighlightB; set { _cb.HighlightB = value; UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.MidPosition)]
        public float MidPosition
        {
            get => _cb.MidPosition;
            set { _cb.MidPosition = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
        }

        public EffectImpl() : base(ShaderResourceUri.Get("Tritone")) { }

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
            outputRect = inputRects.Length > 0 ? inputRects[0] : default;
            outputOpaqueSubRect = default;
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
                inputRects[0] = outputRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public float ShadowR;
            public float ShadowG;
            public float ShadowB;
            public float MidPosition;
            public float MidtoneR;
            public float MidtoneG;
            public float MidtoneB;
            public float Pad1;
            public float HighlightR;
            public float HighlightG;
            public float HighlightB;
            public float Pad2;
        }

        public enum Properties : int
        {
            ShadowR = 0,
            ShadowG = 1,
            ShadowB = 2,
            MidtoneR = 3,
            MidtoneG = 4,
            MidtoneB = 5,
            HighlightR = 6,
            HighlightG = 7,
            HighlightB = 8,
            MidPosition = 9,
        }
    }
}
