using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Stretch
{
    public class StretchCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public bool IsCentering
        {
            set => SetValue((int)EffectImpl.Properties.IsCentering, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsCentering);
        }
        public float X
        {
            set => SetValue((int)EffectImpl.Properties.X, value);
            get => GetFloatValue((int)EffectImpl.Properties.X);
        }
        public float Y
        {
            set => SetValue((int)EffectImpl.Properties.Y, value);
            get => GetFloatValue((int)EffectImpl.Properties.Y);
        }
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        public float StretchLength
        {
            set => SetValue((int)EffectImpl.Properties.StretchLength, value);
            get => GetFloatValue((int)EffectImpl.Properties.StretchLength);
        }
        public float Range
        {
            set => SetValue((int)EffectImpl.Properties.Range, value);
            get => GetFloatValue((int)EffectImpl.Properties.Range);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsCentering)]
            public bool IsCentering
            {
                get
                {
                    return constants.IsCentering;
                }
                set
                {
                    constants.IsCentering = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.X)]
            public float X
            {
                get
                {
                    return constants.X;
                }
                set
                {
                    constants.X = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Y)]
            public float Y
            {
                get
                {
                    return constants.Y;
                }
                set
                {
                    constants.Y = value;
                    UpdateConstants();
                }
            }
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.StretchLength)]
            public float StretchLength
            {
                get
                {
                    return constants.StretchLength;
                }
                set
                {
                    constants.StretchLength = Math.Max(value, 0);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Range)]
            public float Range
            {
                get
                {
                    return constants.Range;
                }
                set
                {
                    constants.Range = Math.Max(value, 0);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("Stretch"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];
                var LeftTop = GetOutputPoint(new Vector2(inputRect.Left, inputRect.Top));
                var RightTop = GetOutputPoint(new Vector2(inputRect.Right, inputRect.Top));
                var LeftBottom = GetOutputPoint(new Vector2(inputRect.Left, inputRect.Bottom));
                var RightBottom = GetOutputPoint(new Vector2(inputRect.Right, inputRect.Bottom));

                outputRect = new RawRect(
                    (int)Math.Floor(Math.Min(LeftTop.X, LeftBottom.X)),
                    (int)Math.Floor(Math.Min(LeftTop.Y, RightTop.Y)),
                    (int)Math.Ceiling(Math.Max(RightTop.X, RightBottom.X)),
                    (int)Math.Ceiling(Math.Max(LeftBottom.Y, RightBottom.Y)));
                outputOpaqueSubRect = default;
            }
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var LeftTop = GetInputPoint(new Vector2(outputRect.Left, outputRect.Top), false, false);
                var RightTop = GetInputPoint(new Vector2(outputRect.Right, outputRect.Top), true, false);
                var LeftBottom = GetInputPoint(new Vector2(outputRect.Left, outputRect.Bottom), false, true);
                var RightBottom = GetInputPoint(new Vector2(outputRect.Right, outputRect.Bottom), true, true);

                inputRects[0] = new RawRect(
                    (int)Math.Floor(Math.Min(LeftTop.X, LeftBottom.X)),
                    (int)Math.Floor(Math.Min(LeftTop.Y, RightTop.Y)),
                    (int)Math.Ceiling(Math.Max(RightTop.X, RightBottom.X)),
                    (int)Math.Ceiling(Math.Max(LeftBottom.Y, RightBottom.Y)));
            }
            Vector2 GetOutputPoint(Vector2 inputPoint)
            {
                float minClamp = IsCentering ? -0.5f : 0f;
                float maxClamp = IsCentering ? 0.5f : 1.0f;
                float rotatedY = -((inputPoint.X - X) * MathF.Sin(-Angle) + (inputPoint.Y - Y) * MathF.Cos(-Angle));
                float factor;
                if (Range > 0)
                {
                    factor = Math.Clamp(rotatedY / Range, minClamp, maxClamp);
                }
                else
                {
                    if (rotatedY > 0)
                    {
                        factor = maxClamp;
                    }
                    else if(rotatedY < 0)
                    {
                        factor = minClamp;
                    }
                    else
                    {
                        factor = 0;
                    }
                }
                Vector2 shift = factor * StretchLength * new Vector2(MathF.Sin(-Angle), MathF.Cos(-Angle));
                return inputPoint - shift;
            }
            Vector2 GetInputPoint(Vector2 outputPoint, bool isPositiveX, bool isPositiveY)
            {
                if (StretchLength + Range <= 0)
                {
                    return outputPoint;
                }
                Vector2 direction = new(MathF.Sin(-Angle), MathF.Cos(-Angle));
                float minClamp = IsCentering ? -0.5f : 0f;
                float maxClamp = IsCentering ? 0.5f : 1.0f;
                float rotatedY = -((outputPoint.X - X) * MathF.Sin(-Angle) + (outputPoint.Y - Y) * MathF.Cos(-Angle));
                float factor = rotatedY / (StretchLength + Range);
                Vector2 shift;
                if (factor >= maxClamp)
                {
                    shift = maxClamp * StretchLength * direction;
                }
                else if(factor < minClamp)
                {
                    shift = minClamp * StretchLength * direction;
                }
                else
                {
                    Vector2 antialiasingExtention = new((direction.X != 0 ? 1 : 0) * (isPositiveX ? 1 : -1), (direction.Y != 0 ? 1 : 0) * (isPositiveY ? 1 : -1));
                    shift = factor * StretchLength * direction + antialiasingExtention;
                }
                return outputPoint + shift;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public bool IsCentering;
                public float X;
                public float Y;
                public float Angle;
                public float StretchLength;
                public float Range;
            }
            public enum Properties : int
            {
                IsCentering = 0,
                X = 1,
                Y = 2,
                Angle = 3,
                StretchLength = 4,
                Range = 5,
            }
        }
    }
}
