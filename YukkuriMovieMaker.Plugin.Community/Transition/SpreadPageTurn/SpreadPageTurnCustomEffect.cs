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

                //めくれたページは中央の折り目を越えて反対側の半面に着地するだけで
                //入力矩形の外には出ないため、出力矩形の拡張は不要
                outputRect = lastOutputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                //めくり返し面が折り目を挟んだ任意の位置を参照するため、出力全域を無効化する
                return lastOutputRect;
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
                public float Pad0;
                public float Pad1;
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
            }
        }
    }
}
