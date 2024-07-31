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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LensBlur
{
    internal class LensBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Radius
        {
            get => GetFloatValue((int)EffectImpl.Properties.Radius);
            set => SetValue((int)EffectImpl.Properties.Radius, value);
        }
        public float Brightness
        {
            get => GetFloatValue((int)EffectImpl.Properties.Brightness);
            set => SetValue((int)EffectImpl.Properties.Brightness, value);
        }
        public float Quality
        {
            get => GetFloatValue((int)EffectImpl.Properties.Quality);
            set => SetValue((int)EffectImpl.Properties.Quality, value);
        }
        public float EdgeStrength
        {
            get => GetFloatValue((int)EffectImpl.Properties.EdgeStrength);
            set => SetValue((int)EffectImpl.Properties.EdgeStrength, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Radius)]
            public float Radius
            {
                get
                {
                    return constants.Radius;
                }
                set
                {
                    constants.Radius = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Brightness)]
            public float Brightness
            {
                get
                {
                    return constants.Brightness;
                }
                set
                {
                    constants.Brightness = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Quality)]
            public float Quality
            {
                get
                {
                    return constants.Quality;
                }
                set
                {
                    constants.Quality = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.EdgeStrength)]
            public float EdgeStrength
            {
                get
                {
                    return constants.EdgeStrength;
                }
                set
                {
                    constants.EdgeStrength = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("LensBlur"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var range = (int)Math.Ceiling(constants.Radius);
                outputRect = new RawRect(
                    inputRects[0].Left - range,
                    inputRects[0].Top - range, 
                    inputRects[0].Right + range, 
                    inputRects[0].Bottom + range);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var range = (int)Math.Ceiling(constants.Radius);
                inputRects[0] = new RawRect(
                    outputRect.Left - range,
                    outputRect.Top - range,
                    outputRect.Right + range,
                    outputRect.Bottom + range);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Radius;
                public float Brightness;
                public float EdgeStrength;
                public float Quality;
            }
            public enum Properties : int
            {
                Radius,
                Brightness,
                EdgeStrength,
                Quality,
            }
        }
    }
}
