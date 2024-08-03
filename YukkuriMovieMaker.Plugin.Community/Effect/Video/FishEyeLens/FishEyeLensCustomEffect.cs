using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1;
using Vortice;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;
using System.Numerics;
using WinRT;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FishEyeLens
{
    internal class FishEyeLensCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Angle
        {
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
            set => SetValue((int)EffectImpl.Properties.Angle, value);
        }
        public float Zoom
        {
            get => GetFloatValue((int)EffectImpl.Properties.Zoom);
            set => SetValue((int)EffectImpl.Properties.Zoom, value);
        }
        public Vector4 Rect
        {
            get => GetVector4Value((int)EffectImpl.Properties.Rect);
            set => SetValue((int)EffectImpl.Properties.Rect, value);
        }
        public ProjectionMode Projection
                    {
            get => (ProjectionMode)GetIntValue((int)EffectImpl.Properties.Projection);
            set => SetValue((int)EffectImpl.Properties.Projection, (int)value);
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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Zoom)]
            public float Zoom
            {
                get
                {
                    return constants.Zoom;
                }
                set
                {
                    constants.Zoom = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Rect)]
            public Vector4 Rect
            {
                get
                {
                    return constants.Rect;
                }
                set
                {
                    constants.Rect = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Projection)]
            public int Projection
            {
                get
                {
                    return constants.Projection;
                }
                set
                {
                    constants.Projection = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("FishEyeLens"))
            {

            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect = inputRects[0];
                var points = new[] 
                {
                    new Vector2(rect.Left, rect.Top),
                    new Vector2(rect.Right, rect.Top),
                    new Vector2(rect.Right, rect.Bottom),
                    new Vector2(rect.Left, rect.Bottom),
                };
                var size = points.Max(p => p.Length());
                var projection = constants.Projection;

                if (Math.Abs(constants.Angle) < 0.04)
                {
                    var left = Math.Min(0, rect.Left);
                    var top = Math.Min(0, rect.Top);
                    var right = Math.Max(1, rect.Right);
                    var bottom = Math.Max(1, rect.Bottom);
                    outputRect = new RawRect(left, top, right, bottom);
                    outputOpaqueSubRect = default;
                    return;
                }
                else if(constants.Angle > 0)
                {
                    var ltrb = new[] 
                    {
                        new Vector2(rect.Left, 0),
                        new Vector2(0, rect.Top),
                        new Vector2(rect.Right, 0),
                        new Vector2(0, rect.Bottom)
                    };
                    var f = FishEyeMethods.CalcF(size, constants.Angle, projection);
                    var size2 = FishEyeMethods.Convert(constants.Angle, f, projection);
                    var rate = size / size2;

                    var ltrb2 = ltrb
                        .Select(p =>
                        {
                            var d = p.Length();
                            if (d is 0)
                                return Vector2.Zero;
                            var t = FishEyeMethods.CalcT(d, f, -projection);
                            var d2 = FishEyeMethods.Convert(t, f, projection);
                            return Vector2.Normalize(p) * d2 * rate;
                        }).ToArray();

                    outputRect = new RawRect(
                        (int)Math.Floor(Math.Min(0, ltrb2[0].X)),
                        (int)Math.Floor(Math.Min(0, ltrb2[1].Y)),
                        (int)Math.Ceiling(Math.Max(0, ltrb2[2].X)),
                        (int)Math.Ceiling(Math.Max(0, ltrb2[3].Y)));
                    outputOpaqueSubRect = default;
                }
                else
                {
                    var f = FishEyeMethods.CalcF(size, MathF.Abs(constants.Angle), -projection);
                    var size2 = FishEyeMethods.Convert(MathF.Abs(constants.Angle), f, -projection);
                    var rate = size2 / size;

                    var converted = points.Select(p => p * rate).ToArray();

                    var left = (int)MathF.Floor(Math.Max(-4096, Math.Min(0, rect.Left * rate)));
                    var top = (int)MathF.Floor(Math.Max(-4096, Math.Min(0, rect.Top * rate)));
                    var right = (int)MathF.Ceiling(Math.Min(4096, Math.Max(0, rect.Right * rate)));
                    var bottom = (int)MathF.Ceiling(Math.Min(4096, Math.Max(0, rect.Bottom * rate)));

                    outputRect = new RawRect(
                        left,
                        top,
                        right,
                        bottom);
                    outputOpaqueSubRect = default;
                }
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var rect = constants.Rect;
                inputRect = new RawRect(
                    (int)Math.Floor(rect.X) -1,
                    (int)Math.Floor(rect.Y) -1,
                    (int)Math.Ceiling(rect.Z) + 1,
                    (int)Math.Ceiling(rect.W) + 1);
                inputRects[0] = ClampInputRect(inputRect);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 Rect;
                public float Angle;
                public int Projection;
                public float Zoom;
            }
            public enum Properties : int
            {
                Angle,
                Zoom,
                Projection,
                Rect,
            }

            class FishEyeMethods
            {
                public static float Convert(float t, float f, int mode)
                {
                    return mode switch
                    {
                        //正射影
                        1 => f * MathF.Sin(t),
                        //立体射影
                        2 => 2 * f * MathF.Tan(t / 2),
                        //等距離射影
                        3 => f * t,
                        //等立体角射影
                        4 => 2 * f * MathF.Sin(t / 2),
                        //逆変換
                        < 0 => f * MathF.Tan(t),
                        _ => 0
                    };
                }
                public static float CalcF(float d, float t, int mode)
                {
                    return mode switch
                    {
                        //正変換
                        > 0 => d / MathF.Tan(t),
                        //正射影逆変換
                        -1 => d / MathF.Sin(t),
                        //立体射影逆変換
                        -2 => d / 2 / MathF.Tan(t / 2),
                        //等距離射影逆変換
                        -3 => d / t,
                        //等立体角射影逆変換
                        -4 => d / 2 / MathF.Sin(t / 2),
                        _ => 0,
                    };
                }
                public static float CalcT(float d, float f, int mode)
                {
                    return mode switch
                    {
                        //正射影
                        1 => MathF.Asin(d / f),
                        //立体射影
                        2 => 2 * MathF.Atan(d / 2 / f),
                        //等距離射影
                        3 => d / f,
                        //等立体角射影
                        4 => 2 * MathF.Asin(d / 2 / f),
                        //逆変換
                        < 0 => MathF.Atan(d / f),
                        _ => 0,
                    };
                }
            }
        }
    }
}
