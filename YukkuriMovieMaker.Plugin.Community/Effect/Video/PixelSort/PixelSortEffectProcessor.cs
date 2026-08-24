using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PixelSort
{
    /// <summary>
    /// ピクセルソートのProcessor。
    /// コンピュートシェーダー(PixelSortCompute)にパラメータを渡すだけの薄い実装。
    /// cs_5_0非対応環境(FeatureLevel 11未満)ではパススルーになる。
    /// 並べ替えはステートレスで、フレームごとの入力画像のみから決まる。
    /// </summary>
    class PixelSortEffectProcessor(IGraphicsDevicesAndContext devices, PixelSortEffect item) : VideoEffectProcessorBase(devices)
    {
        PixelSortCompute pixelSort = null!;

        /// <summary>エフェクトが有効か(コンピュートシェーダー非対応環境ではfalse。テスト用)</summary>
        internal bool IsEffectEnabled => !IsPassThroughEffect;

        bool isFirst = true;
        float dirX;
        float dirY;
        float spanLength;
        float thresholdLow;
        float thresholdHigh;
        float strength;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            pixelSort = new PixelSortCompute(devices);
            if (!pixelSort.IsEnabled)
            {
                pixelSort.Dispose();
                pixelSort = null!;
                return null;
            }
            disposer.Collect(pixelSort);

            var effectOutput = pixelSort.Output;
            disposer.Collect(effectOutput);
            return effectOutput;
        }

        protected override void setInput(ID2D1Image? input)
        {
            pixelSort?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            pixelSort?.SetInput(0, null, true);
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            //コンピュートシェーダー非対応環境用
            if (IsPassThroughEffect)
                return desc;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var (dirX, dirY) = item.Direction switch
            {
                PixelSortDirection.Up => (0f, -1f),
                PixelSortDirection.Right => (1f, 0f),
                PixelSortDirection.Left => (-1f, 0f),
                _ => (0f, 1f),
            };
            var spanLength = (float)item.SpanLength.GetValue(frame, length, fps);
            var thresholdLow = (float)(item.ThresholdLow.GetValue(frame, length, fps) / 100);
            var thresholdHigh = (float)(item.ThresholdHigh.GetValue(frame, length, fps) / 100);
            var strength = (float)(item.Strength.GetValue(frame, length, fps) / 100);

            if (isFirst || this.dirX != dirX)
                pixelSort.DirX = dirX;
            if (isFirst || this.dirY != dirY)
                pixelSort.DirY = dirY;
            if (isFirst || this.spanLength != spanLength)
                pixelSort.SpanLength = spanLength;
            if (isFirst || this.thresholdLow != thresholdLow)
                pixelSort.ThresholdLow = thresholdLow;
            if (isFirst || this.thresholdHigh != thresholdHigh)
                pixelSort.ThresholdHigh = thresholdHigh;
            if (isFirst || this.strength != strength)
                pixelSort.Strength = strength;

            isFirst = false;
            this.dirX = dirX;
            this.dirY = dirY;
            this.spanLength = spanLength;
            this.thresholdLow = thresholdLow;
            this.thresholdHigh = thresholdHigh;
            this.strength = strength;

            return desc;
        }
    }
}
