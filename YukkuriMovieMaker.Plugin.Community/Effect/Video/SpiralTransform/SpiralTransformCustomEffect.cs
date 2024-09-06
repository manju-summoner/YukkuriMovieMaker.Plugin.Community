using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpiralTransform
{
    public class SpiralTransformCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        public bool IsRotateOuter
        {
            set => SetValue((int)EffectImpl.Properties.IsRotateOuter, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsRotateOuter);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Angle
            {
                get
                {
                    return constants.Angle;
                }
                set
                {
                    constants.Angle = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsRotateOuter)]
            public bool IsRotateOuter
            {
                get
                {
                    return constants.IsRotateOuter;
                }
                set
                {
                    constants.IsRotateOuter = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("SpiralTransform"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];

                var radius =
                    new[]
                    {
                        new Vector2(inputRect.Left, inputRect.Top),
                        new Vector2(inputRect.Right, inputRect.Top),
                        new Vector2(inputRect.Left, inputRect.Bottom),
                        new Vector2(inputRect.Right, inputRect.Bottom)
                    }
                    .Select(x => x.Length())
                    .Select(x => (int)MathF.Ceiling(x))
                    .Max();

                radius = Math.Min(radius, 2048);
                if (constants.MaxRadius != radius)
                {
                    constants.MaxRadius = radius;
                    UpdateConstants();
                }

                outputRect = new RawRect(-radius, -radius, radius, radius);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var radius =
                    new[]
                    {
                        new Vector2(outputRect.Left, outputRect.Top),
                        new Vector2(outputRect.Right, outputRect.Top),
                        new Vector2(outputRect.Left, outputRect.Bottom),
                        new Vector2(outputRect.Right, outputRect.Bottom)
                    }
                    .Select(x => x.Length())
                    .Select(x => (int)MathF.Ceiling(x))
                    .Max();
                radius = Math.Min(radius, 2048);
                inputRects[0] = new RawRect(-radius, -radius, radius, radius);
            }

            struct ConstantBuffer
            {
                public float Angle;
                public float MaxRadius;
                public bool IsRotateOuter;
            }
            public enum Properties : int
            {
                Angle = 0,
                IsRotateOuter = 1,
            }
        }
    }
}
