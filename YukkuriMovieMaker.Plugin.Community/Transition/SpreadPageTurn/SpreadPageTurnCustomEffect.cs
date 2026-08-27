using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    internal sealed class SpreadPageTurnCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
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
        public int Page
        {
            set => SetValue((int)EffectImpl.Properties.Page, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Page);
        }
        public int BackMode
        {
            set => SetValue((int)EffectImpl.Properties.BackMode, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.BackMode);
        }
        public int Style
        {
            set => SetValue((int)EffectImpl.Properties.Style, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Style);
        }
        public float InvDistance
        {
            set => SetValue((int)EffectImpl.Properties.InvDistance, value);
            get => GetFloatValue((int)EffectImpl.Properties.InvDistance);
        }

        //本体のカメラエフェクト（CameraFovEffect）の既定視野角と同じ値（internalのため参照できず複製）
        public const double DefaultFovDegrees = 56.7380926;

        //折りたたみの視野角（°）を仮想カメラ距離の逆数（px^-1）へ変換する。
        //カメラエフェクト（CameraFovEffect）と同じ基準: 距離 = 画面高さ/2 ÷ tan(視野角/2)、
        //0.1°未満は平行投影（0）。ただし異常値（非有限・screenHeight≤0）は本体と違い
        //平行投影へ倒す。エフェクト版はアイテムローカル座標で動くため、遠近の一致は
        //アイテム拡大率100%基準の近似になる
        public static float CalculateInvDistance(double fovDegrees, int screenHeight)
        {
            if (!double.IsFinite(fovDegrees))
                return 0f;
            fovDegrees = Math.Clamp(fovDegrees, 0d, 179.9d);
            if (fovDegrees < 0.1 || screenHeight <= 0)
                return 0f;
            var invDistance = Math.Tan(fovDegrees * Math.PI / 360d) / (screenHeight / 2d);
            return double.IsFinite(invDistance) && invDistance >= 0 ? (float)invDistance : 0f;
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;
            //MapInputRectsToOutputRectより先にMapInvalidRectが呼ばれても
            //無効化漏れにならないよう、初期値は全域相当に倒す
            RawRect lastOutputRect = new(-1_000_000, -1_000_000, 1_000_000, 1_000_000);

            //実デバイスのテクスチャ上限。シェーダーはps_4_1のためFEATURE_LEVEL_10_1
            //（上限8192px）でも動作対象になる。既定は保守的に8192とし、Initializeで
            //実デバイスの対応レベルに応じて更新する
            int maxTextureSize = 8192;

            public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
            {
                base.Initialize(effectContext, transformGraph);
                try
                {
                    Vortice.Direct3D.FeatureLevel[] levels = [Vortice.Direct3D.FeatureLevel.Level_11_0, Vortice.Direct3D.FeatureLevel.Level_10_1];
                    var level = effectContext.GetMaximumSupportedFeatureLevel(levels, levels.Length);
                    maxTextureSize = level >= Vortice.Direct3D.FeatureLevel.Level_11_0 ? 16384 : 8192;
                }
                catch
                {
                    //取得できない場合は保守的な既定値のまま
                }
            }

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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Page)]
            public float Page
            {
                get => constants.Page;
                set
                {
                    constants.Page = Math.Clamp(value, 0f, 3f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.BackMode)]
            public float BackMode
            {
                get => constants.BackMode;
                set
                {
                    constants.BackMode = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Style)]
            public float Style
            {
                get => constants.Style;
                set
                {
                    constants.Style = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.InvDistance)]
            public float InvDistance
            {
                get => constants.InvDistance;
                set
                {
                    constants.InvDistance = Math.Max(value, 0f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("SpreadPageTurn"))
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

                //カール時はめくれたページが常に入力矩形内に収まるため拡張は不要。
                //折りたたみ時は遠近で板が拡大されて入力矩形の外へはみ出すため、その分を広げる
                outputRect = lastOutputRect = ExpandOutputRect(inputRect);
                outputOpaqueSubRect = default;
            }

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                //めくり返し面が折り目を挟んだ任意の位置を参照するため、出力全域を無効化する
                return lastOutputRect;
            }

            //折りたたみの遠近投影では、起き上がった板が拡大されて入力矩形の外へはみ出す。
            //自由端の拡大率は 1/(1-S·sinθ·invD)（invD=カメラ距離の逆数）で、主軸方向は
            //板が倒れている側へその超過分、交差軸方向は両側へ (拡大率-1)×C/2 まで広がる
            RawRect ExpandOutputRect(RawRect rect)
            {
                //トランジションでも入力境界は画面フレームとは限らない（既定の背景色は透明で、
                //入力はアイテム群のバウンディングボックスになる）ため、モードによらず拡張する。
                //画面外へはみ出した出力はD2Dの要求駆動レンダリングで実体化されないので無駄にならない
                if (constants.Style < 0.5f || constants.InvDistance <= 0f)
                    return rect;

                var w = (float)(rect.Right - rect.Left);
                var h = (float)(rect.Bottom - rect.Top);
                var horizontal = constants.Page < 1.5f;
                var s = (horizontal ? w : h) * 0.5f;
                var c = horizontal ? h : w;
                if (s <= 0f)
                    return rect;

                var theta = MathF.PI * constants.Progress;
                var sinT = MathF.Sin(theta);
                var cosT = MathF.Cos(theta);
                //板の先端がカメラ面へ近づくと拡大率が発散するため、拡張計算上は10倍で頭打ちにする
                var edgeScale = 1f / MathF.Max(1f - s * sinT * constants.InvDistance, 0.1f);
                //中間サーフェスが実デバイスのテクスチャ上限を超えて確保に失敗しないよう、
                //最終的な出力寸法が上限に収まる範囲で頭打ちにする
                //（固定値で切ると大きな素材+高遠近で、まだ見える投影面が不要にクリップされる）
                var eMainMax = Math.Max(maxTextureSize - (int)MathF.Ceiling(2f * s), 0);
                var eCrossMax = Math.Max((maxTextureSize - (int)MathF.Ceiling(c)) / 2, 0);
                //AA用の+2pxは実際に投影が矩形外へ出るときだけ足す。無条件に足すと
                //進行度0/1（登場退場版では効果時間外の全区間）でも出力境界が広がり、
                //入力境界から寸法を計算する後段エフェクトへ影響する
                var mainOverhang = s * (MathF.Abs(cosT) * edgeScale - 1f);
                var crossOverhang = 0.5f * c * (edgeScale - 1f);
                var eMain = mainOverhang > 0.5f ? Math.Min((int)MathF.Ceiling(mainOverhang) + 2, eMainMax) : 0;
                var eCross = crossOverhang > 0.5f ? Math.Min((int)MathF.Ceiling(crossOverhang) + 2, eCrossMax) : 0;

                //主軸方向は板が倒れている側（起き上がり中はめくる側、倒れ込み中は反対側）だけ広げる
                var toTurning = cosT >= 0f;
                int left = 0, top = 0, right = 0, bottom = 0;
                switch ((int)constants.Page)
                {
                    case 0: if (toTurning) right = eMain; else left = eMain; top = bottom = eCross; break;  //右のページ
                    case 1: if (toTurning) left = eMain; else right = eMain; top = bottom = eCross; break;  //左のページ
                    case 2: if (toTurning) bottom = eMain; else top = eMain; left = right = eCross; break;  //下のページ
                    default: if (toTurning) top = eMain; else bottom = eMain; left = right = eCross; break; //上のページ
                }
                return new RawRect(rect.Left - left, rect.Top - top, rect.Right + right, rect.Bottom + bottom);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //めくり返し面は折り目を挟んだ反対側の任意の位置を参照するため、入力全域を要求する。
                //シェーダーは入力矩形の外をサンプルしない（透明扱い）。
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
                public float Page;
                public float BackMode;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float Style;
                public float InvDistance;
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
                Page = 4,
                BackMode = 5,
                Style = 6,
                InvDistance = 7,
            }
        }
    }
}
