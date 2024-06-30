using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.UnidirectionalBlur
{

    internal class UnidirectionalBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
            set => SetValue((int)EffectImpl.Properties.Angle, value);
        }
        public int Length
        {
            get => GetIntValue((int)EffectImpl.Properties.Length);
            set => SetValue((int)EffectImpl.Properties.Length, value);
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Length)]
            public int Length
            {
                get
                {
                    return constants.Length;
                }
                set
                {
                    constants.Length = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("UnidirectionalBlur"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var direction = Vector2.Transform(new Vector2(constants.Length, 0), Matrix3x2.CreateRotation(constants.Angle));

                var left = (int)Math.Ceiling(Math.Min(inputRects[0].Left, inputRects[0].Left + direction.X));
                var top = (int)Math.Ceiling(Math.Min(inputRects[0].Top, inputRects[0].Top + direction.Y));
                var right = (int)Math.Ceiling(Math.Max(inputRects[0].Right, inputRects[0].Right + direction.X));
                var bottom = (int)Math.Ceiling(Math.Max(inputRects[0].Bottom, inputRects[0].Bottom + direction.Y));

                outputRect = new RawRect(left, top, right, bottom);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var direction = Vector2.Transform(new Vector2(constants.Length, 0), Matrix3x2.CreateRotation(constants.Angle));

                var left = (int)Math.Ceiling(Math.Min(outputRect.Left, outputRect.Left - direction.X));
                var top = (int)Math.Ceiling(Math.Min(outputRect.Top, outputRect.Top - direction.Y));
                var right = (int)Math.Ceiling(Math.Max(outputRect.Right, outputRect.Right - direction.X));
                var bottom = (int)Math.Ceiling(Math.Max(outputRect.Bottom, outputRect.Bottom - direction.Y));

                inputRects[0] = new RawRect(left, top, right, bottom);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Angle;
                public int Length;
            }
            public enum Properties : int
            {
                Angle,
                Length,
            }
        }
    }
}