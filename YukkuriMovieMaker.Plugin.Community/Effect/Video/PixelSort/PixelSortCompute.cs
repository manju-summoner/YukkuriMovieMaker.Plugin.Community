using System;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PixelSort
{
    /// <summary>
    /// ピクセルソートのD2Dカスタムエフェクト。
    /// groupshared上の並列ソートにより、区間の長さに関わらず1ピクセルあたり
    /// ほぼ定数コストで動作する(詳細はPixelSortCS.hlsl参照)。
    /// cs_5_0非対応環境(FeatureLevel 11未満)ではIsEnabledがfalseになる。
    /// </summary>
    sealed class PixelSortCompute(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float DirX
        {
            set => SetValue((int)EffectImpl.Properties.DirX, value);
        }
        public float DirY
        {
            set => SetValue((int)EffectImpl.Properties.DirY, value);
        }
        public float SpanLength
        {
            set => SetValue((int)EffectImpl.Properties.SpanLength, value);
        }
        public float ThresholdLow
        {
            set => SetValue((int)EffectImpl.Properties.ThresholdLow, value);
        }
        public float ThresholdHigh
        {
            set => SetValue((int)EffectImpl.Properties.ThresholdHigh, value);
        }
        public float Strength
        {
            set => SetValue((int)EffectImpl.Properties.Strength, value);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomComputeShaderEffectImplBase<EffectImpl>, ID2D1ComputeTransform
        {
            //PixelSortCS.hlslの定義と一致させる
            const int Threads = 256;
            const int MaxSpan = 4096;

            float dirX = 0f;
            float dirY = 1f;
            float spanLength = MaxSpan;
            float thresholdLow = 0.3f;
            float thresholdHigh = 0.9f;
            float strength = 1f;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DirX)]
            public float DirX
            {
                get => dirX;
                set
                {
                    dirX = Math.Clamp(value, -1f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.DirY)]
            public float DirY
            {
                get => dirY;
                set
                {
                    dirY = Math.Clamp(value, -1f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.SpanLength)]
            public float SpanLength
            {
                get => spanLength;
                set
                {
                    spanLength = Math.Clamp(value, 2f, MaxSpan);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ThresholdLow)]
            public float ThresholdLow
            {
                get => thresholdLow;
                set
                {
                    thresholdLow = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ThresholdHigh)]
            public float ThresholdHigh
            {
                get => thresholdHigh;
                set
                {
                    thresholdHigh = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Strength)]
            public float Strength
            {
                get => strength;
                set
                {
                    strength = Math.Clamp(value, 0f, 1f);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PixelSortCS")) { }

            protected override void UpdateConstants()
            {
                if (computeInformation is null)
                    return;

                //HLSL側cbuffer(b1)のレジスタ配置と一致させる(PixelSortCS.hlsl参照)
                var buffer = new float[]
                {
                    //imageRect
                    inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom,
                    //dirSpan: 軸(0=横,1=縦), 降順フラグ, 区間最大長, 強さ
                    dirY != 0 ? 1f : 0f, dirX + dirY < 0 ? 1f : 0f, spanLength, strength,
                    //threshold
                    thresholdLow, thresholdHigh, 0f, 0f,
                };
                var bytes = MemoryMarshal.AsBytes(buffer.AsSpan()).ToArray();
                computeInformation.SetComputeShaderConstantBuffer(bytes, bytes.Length);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects[0];
                UpdateConstants();
                //ピクセルは画像内で移動するだけなので出力矩形は入力と同じ
                outputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //グループが担当するチャンク範囲全体を読むため、区間最大長ぶん方向軸に沿って膨張させる
                var (marginX, marginY) = GetScanMargin();
                inputRects[0] = new RawRect(
                    outputRect.Left - marginX,
                    outputRect.Top - marginY,
                    outputRect.Right + marginX,
                    outputRect.Bottom + marginY);
            }

            /// <summary>入力の無効領域はスパン走査の届く範囲まで広がって出力へ影響する</summary>
            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                var (marginX, marginY) = GetScanMargin();
                return new RawRect(
                    invalidInputRect.Left - marginX,
                    invalidInputRect.Top - marginY,
                    invalidInputRect.Right + marginX,
                    invalidInputRect.Bottom + marginY);
            }

            public override void CalculateThreadgroups(RawRect outputRect, out int dimensionX, out int dimensionY, out int dimensionZ)
            {
                //シェーダーと同一の式でライン数と担当チャンク範囲を求める(PixelSortCS.hlsl参照)。
                //グループX=ライン(交差軸)、グループY=チャンク集合(方向軸)
                var vertical = dirY != 0;
                int imgAxisMin = vertical ? inputRect.Top : inputRect.Left;
                int outAxisMin = vertical ? outputRect.Top : outputRect.Left;
                int outAxisMax = vertical ? outputRect.Bottom : outputRect.Right;
                int crossLength = vertical ? outputRect.Right - outputRect.Left : outputRect.Bottom - outputRect.Top;

                var chunksPerGroup = Math.Max(1, MaxSpan / ((int)MathF.Ceiling(spanLength) + 1));
                var chunkFirst = (int)MathF.Floor((outAxisMin + 0.5f - imgAxisMin) / spanLength);
                var chunkLast = (int)MathF.Floor((outAxisMax - 0.5f - imgAxisMin) / spanLength);
                var chunkCount = Math.Max(0, chunkLast - chunkFirst + 1);

                dimensionX = Math.Max(1, crossLength);
                dimensionY = Math.Max(1, (chunkCount + chunksPerGroup - 1) / chunksPerGroup);
                dimensionZ = 1;
            }

            (int marginX, int marginY) GetScanMargin()
            {
                var span = (int)MathF.Ceiling(spanLength);
                return (dirX != 0 ? span : 0, dirY != 0 ? span : 0);
            }

            public enum Properties : int
            {
                DirX = 0,
                DirY = 1,
                SpanLength = 2,
                ThresholdLow = 3,
                ThresholdHigh = 4,
                Strength = 5,
            }
        }
    }
}
