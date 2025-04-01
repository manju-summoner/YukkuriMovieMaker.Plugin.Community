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
        readonly ID2D1DeviceContext deviceContext = devices.DeviceContext;
        
        Crop? cropEffect;
        AffineTransform2D? centeringEffect;

        bool isFirst = true;
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
            var width = easedRate * (inputRect.Right - inputRect.Left);
            var height = easedRate * (inputRect.Bottom - inputRect.Top);

            Vector4 cropRect = new(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            Vector2 transform = new(0, 0);

            switch (cropDirection)
            {
                case CropDirection.Left:
                    cropRect.X += width;
                    transform.X = centering ? -(width / 2) : 0;
                    break;
                case CropDirection.Top:
                    cropRect.Y += height;
                    transform.Y = centering ? -(height / 2) : 0;
                    break;
                case CropDirection.Right:
                    cropRect.Z -= width;
                    transform.X = centering ? (width / 2) : 0;
                    break;
                case CropDirection.Bottom:
                    cropRect.W -= height;
                    transform.Y = centering ? (height / 2) : 0;
                    break;
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
                isFirst = false;
            }
            
            return effectDescription.DrawDescription;
        }


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