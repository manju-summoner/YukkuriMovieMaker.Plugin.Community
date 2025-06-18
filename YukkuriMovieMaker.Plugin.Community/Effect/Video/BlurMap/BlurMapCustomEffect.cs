using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.BlurMap
{

    internal class BlurMapCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Blur
        {
            get => GetFloatValue((int)EffectImpl.Properties.Blur); 
            set => SetValue((int)EffectImpl.Properties.Blur, value);
        }

        //ガウスぼかしエフェクトの最大標準偏差が250、半径が750
        //半径[2^0, ... , 2^9, 750]の11段階のぼかしを想定
        //マップ画像、ぼかし無し画像とあわせて13の入力を受け付ける
        [CustomEffect(13)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Blur)]
            public float Blur
            {
                get
                {
                    return constants.Blur;
                }
                set
                {
                    constants.Blur = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("BlurMap"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects[0];
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var rect = outputRect;
                for (int i=0; i < inputRects.Length; i++)
                {
                    inputRects[i] = rect;
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Blur;
            }
            public enum Properties : int
            {
                Blur = 0,
            }
        }
    }
}