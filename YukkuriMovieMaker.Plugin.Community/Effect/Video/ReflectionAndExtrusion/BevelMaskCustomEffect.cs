using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion
{
    internal class BevelAndFlatCompositeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        [CustomEffect(3)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            public EffectImpl() : base(ShaderResourceUri.Get("BevelAndFlatComposite"))
            {

            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = inputRects[1] = inputRects[2] = 
                    new RawRect(
                        outputRect.Left - 1,
                        outputRect.Top -1,
                        outputRect.Right + 1,
                        outputRect.Bottom + 1);
            }

            protected override void UpdateConstants()
            {
            }
        }
    }
}
