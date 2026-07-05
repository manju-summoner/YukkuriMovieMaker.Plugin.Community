using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジットシミュレーション パス1: 仮想ラスター化。
    /// 入力画像(SourceRect)をラスター矩形 {0,0,RasterWidth,RasterHeight} へリサンプルする。
    /// 以降のパスは「x=4fscサンプル番号、y=走査線番号」のラスター空間で動作する。
    /// </summary>
    sealed class NtscRasterize(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>入力画像の論理矩形(シーン座標)。Processorが毎フレームGetImageLocalBoundsで設定する</summary>
        public Vector4 SourceRect
        {
            set => SetValue((int)EffectImpl.Properties.SourceRect, value);
        }
        /// <summary>ラスターの横解像度(有効サンプル数)</summary>
        public float RasterWidth
        {
            set => SetValue((int)EffectImpl.Properties.RasterWidth, value);
        }
        /// <summary>ラスターの縦解像度(走査線数)</summary>
        public float RasterHeight
        {
            set => SetValue((int)EffectImpl.Properties.RasterHeight, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>, ID2D1DrawTransform
        {
            //ダウンサンプル(バイリニア)の読み取り余白
            const int InputMargin = 2;

            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.SourceRect)]
            public Vector4 SourceRect
            {
                get => constants.SourceRect;
                set
                {
                    constants.SourceRect = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RasterWidth)]
            public float RasterWidth
            {
                get => constants.RasterSize.X;
                set
                {
                    constants.RasterSize.X = Math.Max(value, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.RasterHeight)]
            public float RasterHeight
            {
                get => constants.RasterSize.Y;
                set
                {
                    constants.RasterSize.Y = Math.Max(value, 1f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("NtscRasterize")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                //コンポジット信号系のパスは精度が必要なため中間バッファをfloat16にする
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);

                //シェーダーはプールされた中間テクスチャの有効領域外を読まないよう、
                //実際の入力矩形をシーン座標でクランプに使う
                constants.InputRect = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
                UpdateConstants();

                //出力はラスター矩形(原点固定)。座標系がここで切り替わる
                outputRect = new RawRect(0, 0, (int)constants.RasterSize.X, (int)constants.RasterSize.Y);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = MapRasterToSource(outputRect);
            }

            /// <summary>
            /// 入力(ソース座標)の無効領域をラスター座標へ変換する。
            /// 基底クラスの恒等写像では解像度変換で無効領域がずれ、
            /// ダーティレクト部分更新時に古いピクセルが残るため必ず変換する。
            /// </summary>
            public new RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => MapSourceToRaster(invalidInputRect);

            RawRect MapRasterToSource(RawRect rasterRect)
            {
                var src = constants.SourceRect;
                var w = (double)constants.RasterSize.X;
                var h = (double)constants.RasterSize.Y;
                var srcW = (double)(src.Z - src.X);
                var srcH = (double)(src.W - src.Y);
                if (w <= 0 || h <= 0 || srcW <= 0 || srcH <= 0)
                    return rasterRect;
                return new RawRect(
                    (int)Math.Floor(src.X + rasterRect.Left / w * srcW) - InputMargin,
                    (int)Math.Floor(src.Y + rasterRect.Top / h * srcH) - InputMargin,
                    (int)Math.Ceiling(src.X + rasterRect.Right / w * srcW) + InputMargin,
                    (int)Math.Ceiling(src.Y + rasterRect.Bottom / h * srcH) + InputMargin);
            }

            RawRect MapSourceToRaster(RawRect sourceRect)
            {
                var src = constants.SourceRect;
                var w = (double)constants.RasterSize.X;
                var h = (double)constants.RasterSize.Y;
                var srcW = (double)(src.Z - src.X);
                var srcH = (double)(src.W - src.Y);
                if (w <= 0 || h <= 0 || srcW <= 0 || srcH <= 0)
                    return sourceRect;
                return new RawRect(
                    (int)Math.Floor((sourceRect.Left - src.X) / srcW * w) - InputMargin,
                    (int)Math.Floor((sourceRect.Top - src.Y) / srcH * h) - InputMargin,
                    (int)Math.Ceiling((sourceRect.Right - src.X) / srcW * w) + InputMargin,
                    (int)Math.Ceiling((sourceRect.Bottom - src.Y) / srcH * h) + InputMargin);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 InputRect;
                public Vector4 SourceRect;
                public Vector4 RasterSize;
            }
            public enum Properties : int
            {
                SourceRect = 0,
                RasterWidth = 1,
                RasterHeight = 2,
            }
        }
    }
}
