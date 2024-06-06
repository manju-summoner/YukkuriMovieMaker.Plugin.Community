using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeTrimming
{
    internal class EdgeTrimmingCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
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
            public float Value
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

            public EffectImpl() : base(ShaderResourceUri.Get("EdgeTrimming"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //シェーダー内で現在のピクセルの周囲thicknessピクセルを参照するため、
                //inputRectsを上下左右thicknessピクセル分拡張する
                var range = (int)MathF.Ceiling(constants.Thickness);
                inputRects[0] = new Vortice.RawRect(
                    outputRect.Left - range,
                    outputRect.Top - range,
                    outputRect.Right + range,
                    outputRect.Bottom + range);
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
