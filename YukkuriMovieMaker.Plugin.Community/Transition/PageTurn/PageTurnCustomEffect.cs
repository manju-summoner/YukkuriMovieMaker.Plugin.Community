using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    internal sealed class PageTurnCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Progress
        {
            set => SetValue((int)EffectImpl.Properties.Progress, value);
            get => GetFloatValue((int)EffectImpl.Properties.Progress);
        }
        public float Radius
        {
            set => SetValue((int)EffectImpl.Properties.Radius, value);
            get => GetFloatValue((int)EffectImpl.Properties.Radius);
        }
        public float Shadow
        {
            set => SetValue((int)EffectImpl.Properties.Shadow, value);
            get => GetFloatValue((int)EffectImpl.Properties.Shadow);
        }
        public float BackLightness
        {
            set => SetValue((int)EffectImpl.Properties.BackLightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.BackLightness);
        }
        public int Origin
        {
            set => SetValue((int)EffectImpl.Properties.Origin, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Origin);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;
            //MapInputRectsToOutputRectより先にMapInvalidRectが呼ばれても
            //無効化漏れにならないよう、初期値は全域相当に倒す
            RawRect lastOutputRect = new(-1_000_000, -1_000_000, 1_000_000, 1_000_000);

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Progress)]
            public float Progress
            {
                get => constants.Progress;
                set
                {
                    constants.Progress = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Radius)]
            public float Radius
            {
                get => constants.Radius;
                set
                {
                    constants.Radius = Math.Max(value, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Shadow)]
            public float Shadow
            {
                get => constants.Shadow;
                set
                {
                    constants.Shadow = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.BackLightness)]
            public float BackLightness
            {
                get => constants.BackLightness;
                set
                {
                    constants.BackLightness = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Origin)]
            public float Origin
            {
                get => constants.Origin;
                set
                {
                    constants.Origin = Math.Clamp(value, 0f, 7f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PageTurnTransition"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect0 = ClampInputRect(inputRects[0]);
                var rect1 = ClampInputRect(inputRects[1]);

                inputRect = new RawRect(
                    Math.Min(rect0.Left, rect1.Left),
                    Math.Min(rect0.Top, rect1.Top),
                    Math.Max(rect0.Right, rect1.Right),
                    Math.Max(rect0.Bottom, rect1.Bottom));

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = lastOutputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                constants.InputLeft = inputRect.Left;
                constants.InputTop = inputRect.Top;
                constants.InputWidth = inputRect.Right - inputRect.Left;
                constants.InputHeight = inputRect.Bottom - inputRect.Top;

                //中間テクスチャはプールされた大きめのテクスチャが割り当てられることがあり、
                //画像の有効領域はuvの[0,1]と一致しない。シェーダー側でサンプル可否を判定できるよう、
                //各入力の有効矩形（inputRect原点基準のシーン座標）を渡す。
                constants.Input0Left = rect0.Left - inputRect.Left;
                constants.Input0Top = rect0.Top - inputRect.Top;
                constants.Input0Right = rect0.Right - inputRect.Left;
                constants.Input0Bottom = rect0.Bottom - inputRect.Top;
                constants.Input1Left = rect1.Left - inputRect.Left;
                constants.Input1Top = rect1.Top - inputRect.Top;
                constants.Input1Right = rect1.Right - inputRect.Left;
                constants.Input1Bottom = rect1.Bottom - inputRect.Top;
                UpdateConstants();

                outputRect = lastOutputRect = ExpandOutputRect(inputRect);
                outputOpaqueSubRect = default;
            }

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                //めくり返し面が入力画像の任意の位置を参照するため、出力全域を無効化する。
                //出力矩形は入力矩形の外へ拡張されることがあるため、直近に報告した出力矩形を返す。
                return lastOutputRect;
            }

            //めくられたページは入力矩形の外（めくり始めの位置の反対側）へ移動するため、
            //その最大移動量の分だけ出力矩形をめくり方向へ広げる
            RawRect ExpandOutputRect(RawRect rect)
            {
                var w = (float)(rect.Right - rect.Left);
                var h = (float)(rect.Bottom - rect.Top);
                var diag = MathF.Sqrt(w * w + h * h);
                if (diag <= 0f)
                    return rect;

                //めくり方向の単位ベクトルと、めくりが横断する距離
                var (nx, ny, extent) = (int)constants.Origin switch
                {
                    0 => (-w / diag, -h / diag, diag), //右下→左上へ
                    1 => (w / diag, -h / diag, diag),  //左下→右上へ
                    2 => (w / diag, h / diag, diag),   //左上→右下へ
                    3 => (-w / diag, h / diag, diag),  //右上→左下へ
                    4 => (-1f, 0f, w),                 //右辺→左へ
                    5 => (1f, 0f, w),                  //左辺→右へ
                    6 => (0f, 1f, h),                  //上辺→下へ
                    _ => (0f, -1f, h),                 //下辺→上へ
                };
                if (extent <= 0f)
                    return rect;

                var travel = constants.Progress * (extent + 2f * constants.Radius);
                var expansion = MathF.Max(
                    //折り返した紙（fold-back）の最大移動量
                    2f * travel - MathF.PI * constants.Radius,
                    //ロール部分のはみ出し量
                    MathF.Min(travel, extent + MathF.PI * constants.Radius / 2f) - extent);
                if (expansion <= 0f)
                    return rect;

                var dx = (int)MathF.Ceiling((expansion + 2f) * MathF.Abs(nx));
                var dy = (int)MathF.Ceiling((expansion + 2f) * MathF.Abs(ny));
                return new RawRect(
                    rect.Left - (nx < 0f ? dx : 0),
                    rect.Top - (ny < 0f ? dy : 0),
                    rect.Right + (nx > 0f ? dx : 0),
                    rect.Bottom + (ny > 0f ? dy : 0));
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //めくり返し面は入力画像全体の任意の位置を参照するため、入力全域を要求する。
                //シェーダーは入力矩形の外をサンプルしない（透明扱い）ため、出力矩形が
                //入力矩形の外へ拡張されていても入力全域の要求で足りる。
                for (int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Progress;
                public float Radius;
                public float Shadow;
                public float BackLightness;
                public float Origin;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float Pad0;
                public float Pad1;
                public float Pad2;
                public float Input0Left;
                public float Input0Top;
                public float Input0Right;
                public float Input0Bottom;
                public float Input1Left;
                public float Input1Top;
                public float Input1Right;
                public float Input1Bottom;
            }
            public enum Properties : int
            {
                Progress = 0,
                Radius = 1,
                Shadow = 2,
                BackLightness = 3,
                Origin = 4,
            }
        }
    }
}
