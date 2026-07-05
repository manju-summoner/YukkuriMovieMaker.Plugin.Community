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
    /// NTSCコンポジットシミュレーション パス4: 復元。
    /// デコード済みのラスター画像を元の矩形(SourceRect)へ拡大する。
    /// 垂直はガウシアンのビーム断面で走査線構造を再現する。
    /// </summary>
    sealed class NtscRestore(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>出力先の論理矩形(シーン座標)。ラスター化パスのSourceRectと同じ値を渡す</summary>
        public Vector4 SourceRect
        {
            set => SetValue((int)EffectImpl.Properties.SourceRect, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>, ID2D1DrawTransform
        {
            //拡大(バイリニア+3ラインのビーム断面)の読み取り余白(ラスター座標)
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

            public EffectImpl() : base(ShaderResourceUri.Get("NtscRestore")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];

                constants.InputRect = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
                constants.RasterSize = new Vector4(
                    inputRect.Right - inputRect.Left,
                    inputRect.Bottom - inputRect.Top,
                    0, 0);
                UpdateConstants();

                //出力は元画像の矩形。座標系がラスター空間からシーン座標へ戻る
                var src = constants.SourceRect;
                outputRect = new RawRect(
                    (int)Math.Floor(src.X),
                    (int)Math.Floor(src.Y),
                    (int)Math.Ceiling(src.Z),
                    (int)Math.Ceiling(src.W));
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = MapSourceToRaster(outputRect);
            }

            /// <summary>
            /// 入力(ラスター座標)の無効領域をシーン座標へ変換する。
            /// 解像度変換があるため基底クラスの恒等写像のままでは部分更新で破綻する。
            /// </summary>
            public new RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => MapRasterToSource(invalidInputRect);

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

            RawRect MapRasterToSource(RawRect rasterRect)
            {
                var src = constants.SourceRect;
                var w = (double)constants.RasterSize.X;
                var h = (double)constants.RasterSize.Y;
                var srcW = (double)(src.Z - src.X);
                var srcH = (double)(src.W - src.Y);
                if (w <= 0 || h <= 0 || srcW <= 0 || srcH <= 0)
                    return rasterRect;
                //ビーム断面(±1ライン)の影響もラスター側で余白に含めてから変換する
                return new RawRect(
                    (int)Math.Floor(src.X + (rasterRect.Left - InputMargin) / w * srcW),
                    (int)Math.Floor(src.Y + (rasterRect.Top - InputMargin) / h * srcH),
                    (int)Math.Ceiling(src.X + (rasterRect.Right + InputMargin) / w * srcW),
                    (int)Math.Ceiling(src.Y + (rasterRect.Bottom + InputMargin) / h * srcH));
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
            }
        }
    }
}
