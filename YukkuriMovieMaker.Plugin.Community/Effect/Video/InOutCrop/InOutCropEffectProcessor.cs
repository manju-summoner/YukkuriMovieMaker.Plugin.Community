using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InOutCrop
{
    internal class InOutCropEffectProcessor(IGraphicsDevicesAndContext devices, InOutCropEffect item) : VideoEffectProcessorBase(devices)
    {
        /// <summary>
        /// クロップ矩形の四辺を入力の内容より外側へ逃がす量。
        /// BorderMode.Hardはクロップ矩形をピクセル境界へスナップするため、入力のローカル境界を
        /// そのまま指定すると、境界が非整数のとき(奇数サイズ・回転・拡大した画像など)に
        /// アンチエイリアスの1列が削られてしまう。
        /// </summary>
        const float EdgeMargin = 1f;

        readonly ID2D1DeviceContext deviceContext = devices.DeviceContext;

        Crop? cropEffect;
        AffineTransform2D? centeringEffect;

        bool isFirst = true;
        BorderMode borderMode = BorderMode.Soft;
        Vector4 cropRect;
        Vector2 transform;
        AffineTransform2DInterpolationMode interpolationMode;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(cropEffect is null || centeringEffect is null)
                return effectDescription.DrawDescription;

            var interpolationMode = effectDescription.DrawDescription.ZoomInterpolationMode.ToTransform2D();
            var length = effectDescription.ItemDuration.Time.TotalSeconds;
            var firstSeconds = effectDescription.ItemPosition.Time.TotalSeconds;
            var lastSeconds = length - firstSeconds;
            var effectDuration = item.EffectDuration;
            var centering = item.Centering;

            double rate;
            CropDirection cropDirection;

            if (effectDuration * 2 <= length)
            {
                rate = Math.Min(firstSeconds / effectDuration, lastSeconds / effectDuration);
                cropDirection = (firstSeconds <= lastSeconds) ? item.InCropDirection : item.OutCropDirection;
            }
            else
            {
                if ((item.InCropDirection == CropDirection.None) ^ (item.OutCropDirection == CropDirection.None))
                {
                    rate = (item.OutCropDirection == CropDirection.None) ? (firstSeconds / effectDuration) : (lastSeconds / effectDuration);
                    cropDirection = (item.OutCropDirection == CropDirection.None) ? item.InCropDirection : item.OutCropDirection;
                }
                else
                {
                    rate = Math.Min(firstSeconds / effectDuration, lastSeconds / effectDuration);
                    cropDirection = (firstSeconds <= lastSeconds) ? item.InCropDirection : item.OutCropDirection;
                }
            }

            rate = Math.Clamp(rate, 0, 1);
            var easedRate = 1 - (float)Math.Clamp(Easing.GetValue(item.EasingType, item.EasingMode, rate), 0, 1);

            var inputRect = deviceContext.GetImageLocalBounds(input);
            //内容の外側へ逃がした矩形を基準にする。こうすると動く辺も、クリッピングが全開の位置で
            //内容より外側に来るため、スナップで内容が削られない。
            //ただし逃がすとスナップ後の出力バウンディングが最大1px広がり、
            //「画面内に収まるように拡大縮小」や中心点など、バウンディングを見る機能に影響が出る。
            //クリッピングが効いていないフレームでは逃がさず、従来どおり入力の境界をそのまま使う。
            //(登場と退場が重なるほどアイテムが短い場合はクリッピングが途切れないため、
            // 全フレームで逃がした状態になる)
            var isCropping = easedRate > 0 && cropDirection is not CropDirection.None;
            //クリッピングしていないフレームは従来どおりSoft・境界そのままにして、挙動を変えない。
            var borderMode = isCropping ? BorderMode.Hard : BorderMode.Soft;
            var margin = isCropping ? EdgeMargin : 0;
            var contentWidth = inputRect.Right - inputRect.Left;
            var contentHeight = inputRect.Bottom - inputRect.Top;
            var left = inputRect.Left - margin;
            var top = inputRect.Top - margin;
            var right = inputRect.Right + margin;
            var bottom = inputRect.Bottom + margin;
            var width = easedRate * (right - left);
            var height = easedRate * (bottom - top);

            Vector4 cropRect = new(left, top, right, bottom);
            Vector2 transform = new(0, 0);

            switch (cropDirection)
            {
                case CropDirection.Left:
                    cropRect.X += width;
                    transform.X = centering ? -RoundToPixel(CroppedAmount(width, margin, contentWidth) / 2) : 0;
                    break;
                case CropDirection.Top:
                    cropRect.Y += height;
                    transform.Y = centering ? -RoundToPixel(CroppedAmount(height, margin, contentHeight) / 2) : 0;
                    break;
                case CropDirection.Right:
                    cropRect.Z -= width;
                    transform.X = centering ? RoundToPixel(CroppedAmount(width, margin, contentWidth) / 2) : 0;
                    break;
                case CropDirection.Bottom:
                    cropRect.W -= height;
                    transform.Y = centering ? RoundToPixel(CroppedAmount(height, margin, contentHeight) / 2) : 0;
                    break;
            }

            if (isFirst || this.borderMode != borderMode)
            {
                cropEffect.BorderMode = borderMode;
                this.borderMode = borderMode;
            }
            if (isFirst || this.cropRect != cropRect)
            {
                cropEffect.Rectangle = cropRect;
                this.cropRect = cropRect;
            }
            if (isFirst || this.transform != transform || this.interpolationMode != interpolationMode)
            {
                centeringEffect.TransformMatrix = Matrix3x2.CreateTranslation(transform);
                centeringEffect.InterPolationMode = interpolationMode;
                this.transform = transform;
                this.interpolationMode = interpolationMode;
            }
            isFirst = false;

            return effectDescription.DrawDescription;
        }

        /// <summary>
        /// 実際に入力から切り落とされた量。
        /// クロップ矩形の辺は内容よりmarginぶん外側から動き始めるので、その分を差し引く。
        /// 全開のときは辺が内容の外側にあり何も切り落とされない。
        /// 全閉のときは反対側の余白まで進むが、切り落とされる量は内容の幅で頭打ちになる。
        /// </summary>
        static float CroppedAmount(float travel, float margin, float contentSpan)
            => Math.Clamp(travel - margin, 0, contentSpan);

        /// <summary>
        /// センタリングの移動量をピクセル単位に丸める。
        /// クロップの辺はBorderMode.Hardでピクセル境界へスナップされるため、移動量だけを小数のままに
        /// しておくと、後段のAffineTransform2Dが拡大方法の補間でリサンプルしてしまう。
        /// キュービック補間ではエッジでオーバーシュートしてプリマルチプライの値域を外れるうえ、
        /// 辺の位置と移動量がずれてクリッピング中に画像が微妙に揺れる。
        /// 段の幅が不揃いにならないよう、0.5は常に外側へ丸める。
        /// この結果、センタリングの動きは1ドット刻みになる。
        /// </summary>
        static float RoundToPixel(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            cropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);

            centeringEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(centeringEffect);

            using (var image = cropEffect.Output)
            {
                centeringEffect.SetInput(0, image, true);
            }

            var output = centeringEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            cropEffect?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain()
        {
            cropEffect?.SetInput(0, null, true);
            centeringEffect?.SetInput(0, null, true);
        }
    }
}