using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

public sealed class GradientMapCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    public int IsHorizontal
    {
        get => GetIntValue((int)EffectImpl.Properties.IsHorizontal);
        set => SetValue((int)EffectImpl.Properties.IsHorizontal, value);
    }

    public void SetSourceInput(ID2D1Image? image) => SetInput(0, image, true);
    public void SetGradientInput(ID2D1Image? bitmap) => SetInput(1, bitmap, true);

    [CustomEffect(2)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;

        [CustomEffectProperty(PropertyType.Int32, (int)Properties.IsHorizontal)]
        public int IsHorizontal
        {
            get => _cb.IsHorizontal;
            set { _cb.IsHorizontal = value; UpdateConstants(); }
        }

        public EffectImpl() : base(ShaderResourceUri.Get("GradientMap")) { }

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
            if (inputRects.Length > 0) inputRects[0] = outputRect;
            if (inputRects.Length > 1) inputRects[1] = new RawRect(-65536, -65536, 65536, 65536);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public int IsHorizontal;
            public float Pad0;
            public float Pad1;
            public float Pad2;
        }

        public enum Properties
        {
            IsHorizontal
        }
    }
}
