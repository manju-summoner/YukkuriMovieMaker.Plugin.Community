using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VignetteBlur
{

    internal class VignetteCircularBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices)), IVignetteBlurEffect
    {
        public Vector2 Center
        {
            get => GetVector2Value((int)EffectImpl.Properties.Center);
            set => SetValue((int)EffectImpl.Properties.Center, value);
        }

        public float Radius
        {
            get => GetFloatValue((int)EffectImpl.Properties.Radius);
            set => SetValue((int)EffectImpl.Properties.Radius, value);
        }

        public float Aspect
        {
            get => GetFloatValue((int)EffectImpl.Properties.Aspect);
            set => SetValue((int)EffectImpl.Properties.Aspect, value);
        }

        public float Softness
        {
            get => GetFloatValue((int)EffectImpl.Properties.Softness);
            set => SetValue((int)EffectImpl.Properties.Softness, value);
        }

        public bool IsFixedSize
        {
            get => GetBoolValue((int)EffectImpl.Properties.IsFixedSize);
            set => SetValue((int)EffectImpl.Properties.IsFixedSize, value);
        }

        public float Blur
        {
            get => GetFloatValue((int)EffectImpl.Properties.Blur); 
            set => SetValue((int)EffectImpl.Properties.Blur, value);
        }

        public float Lightness
        {
            get => GetFloatValue((int)EffectImpl.Properties.Lightness);
            set => SetValue((int)EffectImpl.Properties.Lightness, value);
        }

        public float ColorShift
        {
            get => GetFloatValue((int)EffectImpl.Properties.ColorShift);
            set => SetValue((int)EffectImpl.Properties.ColorShift, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            bool isFixedSize;
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector2, (int)Properties.Center)]
            public Vector2 Center
            {
                get
                {
                    return constants.Center;
                }
                set
                {
                    constants.Center = value;
                    UpdateConstants();
                }
            }

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

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Aspect)]
            public float Aspect
            {
                get
                {
                    return constants.Aspect;
                }
                set
                {
                    constants.Aspect = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Softness)]
            public float Softness
            {
                get
                {
                    return constants.Softness;
                }
                set
                {
                    constants.Softness = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsFixedSize)]
            public bool IsFixedSize
            {
                get
                {
                    return isFixedSize;
                }
                set
                {
                    isFixedSize = value;
                }
            }

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

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Lightness)]
            public float Lightness
            {
                get
                {
                    return constants.Lightness;
                }
                set
                {
                    constants.Lightness = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorShift)]
            public float ColorShift
            {
                get
                {
                    return constants.ColorShift;
                }
                set
                {
                    constants.ColorShift = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("VignetteCircularBlur"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect = inputRects[0];

                if (IsFixedSize)
                {
                    outputRect = rect;
                }
                else
                {
                    var r =
                        new Vector2[]
                        {
                            new (rect.Left, rect.Top),
                            new (rect.Right, rect.Top),
                            new (rect.Left, rect.Bottom),
                            new (rect.Right, rect.Bottom)
                        }
                        .Select(v => v - Center)
                        .Select(v => v.Length())
                        .Max();

                    outputRect = new RawRect(
                        (int)(Math.Ceiling(Center.X - r)),
                        (int)(Math.Ceiling(Center.Y - r)),
                        (int)(Math.Floor(Center.X + r)),
                        (int)(Math.Floor(Center.Y + r)));

                }
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var rect = outputRect;
                var r =
                    new Vector2[]
                    {
                            new (rect.Left, rect.Top),
                            new (rect.Right, rect.Top),
                            new (rect.Left, rect.Bottom),
                            new (rect.Right, rect.Bottom)
                    }
                    .Select(v => v - Center)
                    .Select(v => v.Length())
                    .Max();

                inputRects[0] = new RawRect(
                    (int)(Math.Ceiling(Center.X - r)),
                    (int)(Math.Ceiling(Center.Y - r)),
                    (int)(Math.Floor(Center.X + r)),
                    (int)(Math.Floor(Center.Y + r)));
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector2 Center;
                public float Radius;
                public float Aspect;
                public float Softness;
                public float Blur;
                public float Lightness;
                public float ColorShift;
            }
            public enum Properties : int
            {
                Center = 0,
                Radius = 1,
                Aspect = 2,
                Softness = 3,
                Blur = 4,
                Lightness = 5,
                ColorShift = 6,

                IsFixedSize = 7,
            }
        }
    }
}