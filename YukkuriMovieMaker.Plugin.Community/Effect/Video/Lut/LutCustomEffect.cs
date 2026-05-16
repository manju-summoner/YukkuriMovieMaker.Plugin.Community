using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

public sealed class LutCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    public float LutSize
    {
        get => GetFloatValue((int)EffectImpl.Properties.LutSize);
        set => SetValue((int)EffectImpl.Properties.LutSize, value);
    }

    public float AtlasWidth
    {
        get => GetFloatValue((int)EffectImpl.Properties.AtlasWidth);
        set => SetValue((int)EffectImpl.Properties.AtlasWidth, value);
    }

    public float AtlasHeight
    {
        get => GetFloatValue((int)EffectImpl.Properties.AtlasHeight);
        set => SetValue((int)EffectImpl.Properties.AtlasHeight, value);
    }

    public int InterpolationMode
    {
        get => GetIntValue((int)EffectImpl.Properties.InterpolationMode);
        set => SetValue((int)EffectImpl.Properties.InterpolationMode, value);
    }

    public void SetDomain(CubeLut lut)
    {
        var (minR, minG, minB) = lut.DomainMin;
        var (scaleR, scaleG, scaleB) = lut.DomainScale;
        ApplyDomain(minR, minG, minB, scaleR, scaleG, scaleB);
    }

    public void SetDomain(float minR, float minG, float minB, float maxR, float maxG, float maxB)
    {
        var scaleR = maxR > minR ? 1f / (maxR - minR) : 1f;
        var scaleG = maxG > minG ? 1f / (maxG - minG) : 1f;
        var scaleB = maxB > minB ? 1f / (maxB - minB) : 1f;
        ApplyDomain(minR, minG, minB, scaleR, scaleG, scaleB);
    }

    private void ApplyDomain(float minR, float minG, float minB, float scaleR, float scaleG, float scaleB)
    {
        SetValue((int)EffectImpl.Properties.DomainMinR, minR);
        SetValue((int)EffectImpl.Properties.DomainMinG, minG);
        SetValue((int)EffectImpl.Properties.DomainMinB, minB);
        SetValue((int)EffectImpl.Properties.DomainScaleR, scaleR);
        SetValue((int)EffectImpl.Properties.DomainScaleG, scaleG);
        SetValue((int)EffectImpl.Properties.DomainScaleB, scaleB);
    }

    public void SetSourceInput(ID2D1Image? image) => SetInput(0, image, true);
    public void SetLutInput(ID2D1Image? bitmap) => SetInput(1, bitmap, true);

    [CustomEffect(2)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb = new() { DomainScaleR = 1f, DomainScaleG = 1f, DomainScaleB = 1f };

        [CustomEffectProperty(PropertyType.Float, (int)Properties.LutSize)]
        public float LutSize
        {
            get => _cb.LutSize;
            set { _cb.LutSize = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.AtlasWidth)]
        public float AtlasWidth
        {
            get => _cb.AtlasWidth;
            set { _cb.AtlasWidth = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.AtlasHeight)]
        public float AtlasHeight
        {
            get => _cb.AtlasHeight;
            set { _cb.AtlasHeight = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Int32, (int)Properties.InterpolationMode)]
        public int InterpolationMode
        {
            get => _cb.InterpolationMode;
            set { _cb.InterpolationMode = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainMinR)]
        public float DomainMinR
        {
            get => _cb.DomainMinR;
            set { _cb.DomainMinR = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainMinG)]
        public float DomainMinG
        {
            get => _cb.DomainMinG;
            set { _cb.DomainMinG = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainMinB)]
        public float DomainMinB
        {
            get => _cb.DomainMinB;
            set { _cb.DomainMinB = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainScaleR)]
        public float DomainScaleR
        {
            get => _cb.DomainScaleR;
            set { _cb.DomainScaleR = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainScaleG)]
        public float DomainScaleG
        {
            get => _cb.DomainScaleG;
            set { _cb.DomainScaleG = value; UpdateConstants(); }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.DomainScaleB)]
        public float DomainScaleB
        {
            get => _cb.DomainScaleB;
            set { _cb.DomainScaleB = value; UpdateConstants(); }
        }

        public EffectImpl() : base(ShaderResourceUri.Get("Lut")) { }

        public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
        {
            base.SetDrawInfo(drawInfo);
            drawInfo?.SetInputDescription(1, new InputDescription
            {
                Filter = Filter.MinMagMipPoint,
                LevelOfDetailCount = 0,
            });
        }

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

        public override void MapOutputRectToInputRects(
            RawRect outputRect,
            RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
                inputRects[0] = outputRect;
            if (inputRects.Length > 1)
            {
                var width = (int)MathF.Max(1f, _cb.AtlasWidth);
                var height = (int)MathF.Max(1f, _cb.AtlasHeight);
                inputRects[1] = new RawRect(0, 0, width, height);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public float LutSize;
            public float AtlasWidth;
            public float AtlasHeight;
            public int InterpolationMode;
            public float DomainMinR;
            public float DomainMinG;
            public float DomainMinB;
            public float Pad0;
            public float DomainScaleR;
            public float DomainScaleG;
            public float DomainScaleB;
            public float Pad1;
        }

        public enum Properties
        {
            LutSize,
            AtlasWidth,
            AtlasHeight,
            InterpolationMode,
            DomainMinR,
            DomainMinG,
            DomainMinB,
            DomainScaleR,
            DomainScaleG,
            DomainScaleB,
        }
    }
}
