using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    internal sealed class ReelSpinTransitionCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>リールの移動量（extent単位）。0でbefore、travel=laps*2-1でafterに着地する。</summary>
        public float Travel
        {
            set => SetValue((int)EffectImpl.Properties.Travel, value);
            get => GetFloatValue((int)EffectImpl.Properties.Travel);
        }
        /// <summary>コンテンツの移動方向（ラジアン、0で右・90°で下）</summary>
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        /// <summary>ブラー長（extent単位）。移動速度×強さから算出した値を渡す。</summary>
        public float Blur
        {
            set => SetValue((int)EffectImpl.Properties.Blur, value);
            get => GetFloatValue((int)EffectImpl.Properties.Blur);
        }
        /// <summary>回転数（1以上）。連続（AAABBB）配置のしきい値に使う。</summary>
        public float Laps
        {
            set => SetValue((int)EffectImpl.Properties.Laps, value);
            get => GetFloatValue((int)EffectImpl.Properties.Laps);
        }
        /// <summary>0:交互（ABAB） 1:連続（AAABBB）</summary>
        public int Pattern
        {
            set => SetValue((int)EffectImpl.Properties.Pattern, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Pattern);
        }
        /// <summary>0:レンガ積み配置（リール方向に整列、斜めでも隙間なし） 1:XY固定格子の敷き詰め</summary>
        public int Tile
        {
            set => SetValue((int)EffectImpl.Properties.Tile, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Tile);
        }
        /// <summary>スクリーンの幅（px）。リールの矩形はスクリーン矩形（原点中心）として扱う。</summary>
        public float ScreenWidth
        {
            set => SetValue((int)EffectImpl.Properties.ScreenWidth, value);
            get => GetFloatValue((int)EffectImpl.Properties.ScreenWidth);
        }
        /// <summary>スクリーンの高さ（px）</summary>
        public float ScreenHeight
        {
            set => SetValue((int)EffectImpl.Properties.ScreenHeight, value);
            get => GetFloatValue((int)EffectImpl.Properties.ScreenHeight);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Travel)]
            public float Travel
            {
                get => constants.Travel;
                set
                {
                    constants.Travel = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Angle)]
            public float Angle
            {
                get => constants.Angle;
                set
                {
                    constants.Angle = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Blur)]
            public float Blur
            {
                get => constants.Blur;
                set
                {
                    //1枚ぶん平均したら見た目はそれ以上変わらないため上限1
                    constants.Blur = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Laps)]
            public float Laps
            {
                get => constants.Laps;
                set
                {
                    constants.Laps = Math.Max(value, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Pattern)]
            public float Pattern
            {
                get => constants.Pattern;
                set
                {
                    constants.Pattern = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Tile)]
            public float Tile
            {
                get => constants.Tile;
                set
                {
                    constants.Tile = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }

            //リール矩形の算出（MapInputRectsToOutputRect）にのみ使うため定数バッファには含めない
            float screenWidth;
            float screenHeight;
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ScreenWidth)]
            public float ScreenWidth
            {
                get => screenWidth;
                set => screenWidth = Math.Max(value, 0f);
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ScreenHeight)]
            public float ScreenHeight
            {
                get => screenHeight;
                set => screenHeight = Math.Max(value, 0f);
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ReelSpinTransition"))
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

                //入力画像の有効矩形はコンテンツに密着していてスクリーンより小さいことがある。
                //リールはスクリーン全体（原点中心）を1枚の画像として回す。
                //スクリーンサイズが未設定の場合のみ入力の結合矩形にフォールバックする
                if (screenWidth >= 1f && screenHeight >= 1f)
                {
                    var sw = (int)MathF.Round(screenWidth);
                    var sh = (int)MathF.Round(screenHeight);
                    inputRect = ClampInputRect(new RawRect(-(sw / 2), -(sh / 2), -(sw / 2) + sw, -(sh / 2) + sh));
                }
                else
                {
                    inputRect = new RawRect(
                        Math.Min(rect0.Left, rect1.Left),
                        Math.Min(rect0.Top, rect1.Top),
                        Math.Max(rect0.Right, rect1.Right),
                        Math.Max(rect0.Bottom, rect1.Bottom));
                }

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
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

                //中身が周回するだけで画像の外へははみ出さないため、出力矩形は入力の結合矩形と同じ
                outputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //周回サンプリングにより出力の任意の位置が入力全体を参照しうるため、入力全域を要求する
                for (int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = inputRect;
            }

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                //入力の一部変更が周回先の任意の位置に現れるため、出力全域を無効化する
                return inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Travel;
                public float Angle;
                public float Blur;
                public float Laps;
                public float Pattern;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float Tile;
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
                Travel = 0,
                Angle = 1,
                Blur = 2,
                Laps = 3,
                Pattern = 4,
                Tile = 5,
                ScreenWidth = 6,
                ScreenHeight = 7,
            }
        }
    }
}
