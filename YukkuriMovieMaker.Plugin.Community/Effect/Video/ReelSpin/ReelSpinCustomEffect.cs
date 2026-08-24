using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReelSpin
{
    internal sealed class ReelSpinCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>回転位置（周）。1で画像1枚ぶん周回する。</summary>
        public float Rotation
        {
            set => SetValue((int)EffectImpl.Properties.Rotation, value);
            get => GetFloatValue((int)EffectImpl.Properties.Rotation);
        }
        /// <summary>コンテンツの移動方向（ラジアン、0で右・90°で下）</summary>
        public float Angle
        {
            set => SetValue((int)EffectImpl.Properties.Angle, value);
            get => GetFloatValue((int)EffectImpl.Properties.Angle);
        }
        /// <summary>ブラー長（周）。回転速度×強さから算出した値を渡す。</summary>
        public float Blur
        {
            set => SetValue((int)EffectImpl.Properties.Blur, value);
            get => GetFloatValue((int)EffectImpl.Properties.Blur);
        }
        /// <summary>0:レンガ積み配置（リール方向に整列、斜めでも隙間なし） 1:XY固定格子の敷き詰め</summary>
        public int Tile
        {
            set => SetValue((int)EffectImpl.Properties.Tile, (float)value);
            get => (int)GetFloatValue((int)EffectImpl.Properties.Tile);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Rotation)]
            public float Rotation
            {
                get => constants.Rotation;
                set
                {
                    constants.Rotation = value;
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
                    //1周ぶん平均したら見た目はそれ以上変わらないため上限1
                    constants.Blur = Math.Clamp(value, 0f, 1f);
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

            public EffectImpl() : base(ShaderResourceUri.Get("ReelSpin"))
            {

            }
            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);

                constants.InputLeft = inputRect.Left;
                constants.InputTop = inputRect.Top;
                constants.InputWidth = inputRect.Right - inputRect.Left;
                constants.InputHeight = inputRect.Bottom - inputRect.Top;
                UpdateConstants();

                //中身が周回するだけで画像の外へははみ出さないため、出力矩形は入力矩形と同じ
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
                public float Rotation;
                public float Angle;
                public float Blur;
                public float Tile;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
            }
            public enum Properties : int
            {
                Rotation = 0,
                Angle = 1,
                Blur = 2,
                Tile = 3,
            }
        }
    }
}
