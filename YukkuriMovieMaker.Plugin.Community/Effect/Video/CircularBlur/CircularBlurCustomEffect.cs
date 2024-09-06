using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CircularBlur
{

    public class CircularBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
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

        public bool IsHardBorder
        {
            set => SetValue((int)EffectImpl.Properties.IsHardBorder, value);
            get => GetBoolValue((int)EffectImpl.Properties.IsHardBorder);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer constants;

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


            [CustomEffectProperty(PropertyType.Bool, (int)Properties.IsHardBorder)]
            public bool IsHardBorder { set; get; }

            public EffectImpl() : base(ShaderResourceUri.Get("CircularBlur")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }
            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect = ClampInputRect(inputRects[0]);
                if (inputRect.Left != rect.Left || inputRect.Top != rect.Top || inputRect.Right != rect.Right || inputRect.Bottom != rect.Bottom)
                {
                    inputRect = rect;
                    constants.Left = inputRect.Left;
                    constants.Top = inputRect.Top;
                    constants.Right = inputRect.Right;
                    constants.Bottom = inputRect.Bottom;
                    UpdateConstants();
                }

                if (IsHardBorder)
                {
                    outputRect = inputRect;
                }
                else
                {
                    var center = new Vector2(inputRect.Left + (inputRect.Right - inputRect.Left) / 2, inputRect.Top + (inputRect.Bottom - inputRect.Top) / 2) + new Vector2(X, Y);
                    var r = new[]
                    {
                        new Vector2(inputRect.Left,inputRect.Top)-center,
                        new Vector2(inputRect.Right,inputRect.Top)-center,
                        new Vector2(inputRect.Right,inputRect.Bottom)-center,
                        new Vector2(inputRect.Left,inputRect.Bottom)-center,
                    }
                    .Select(v => v.Length())
                    .Max();

                    outputRect = new RawRect((int)(center.X - r),
                                             (int)(center.Y - r),
                                             (int)(center.X + r),
                                             (int)(center.Y + r));
                }
                outputOpaqueSubRect = default;
            }


            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Angle;
                public float X;
                public float Y;

                public float Left;
                public float Top;
                public float Right;
                public float Bottom;
            }
            public enum Properties : int
            {
                Angle = 0,
                X = 1,
                Y = 2,
                IsHardBorder = 3
            }
        }
    }
}
