using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.BevelHeightmap
{
    internal class BevelHeightmapCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Thickness
        {
            get=>GetFloatValue((int)EffectImpl.Properties.Thickness);
            set=>SetValue((int)EffectImpl.Properties.Thickness, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Thickness)]
            public float Thickness
            {
                get
                {
                    return constants.Thickness;
                }
                set
                {
                    constants.Thickness = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelHeightmap"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var range = Math.Ceiling(constants.Thickness);
                inputRects[0] = new RawRect(
                    outputRect.Left - (int)range,
                    outputRect.Top - (int)range,
                    outputRect.Right + (int)range,
                    outputRect.Bottom + (int)range);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Thickness;
            }
            public enum Properties : int
            {
                Thickness,
            }
        }
    }
}