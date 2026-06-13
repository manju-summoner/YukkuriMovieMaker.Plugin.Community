using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RatioCrop
{
    internal class RatioCropEffectProcessor : IVideoEffectProcessor
    {
        readonly IGraphicsDevicesAndContext devices;

        readonly RatioCropEffect item;

        ID2D1Image? input;

        readonly DisposeCollector disposer = new();

        readonly Crop crop;
        readonly AffineTransform2D transform;

        bool isFirst = true;
        Vector4 cropRect;
        Vector2 translation;
        AffineTransform2DInterpolationMode interpolationMode;

        public ID2D1Image Output { get; }

        public RatioCropEffectProcessor(IGraphicsDevicesAndContext devices, RatioCropEffect item)
        {
            this.devices = devices;
            this.item = item;

            crop = new Crop(devices.DeviceContext);
            disposer.Collect(crop);
            transform = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(transform);

            using (var image = crop.Output)
            {
                transform.SetInput(0, image, true);
            }

            Output = transform.Output;
            disposer.Collect(Output);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;

            var top = (float)item.Top.GetValue(frame, length, fps) / 100 * height;
            var bottom = (float)item.Bottom.GetValue(frame, length, fps) / 100 * height;
            var left = (float)item.Left.GetValue(frame, length, fps) / 100 * width;
            var right = (float)item.Right.GetValue(frame, length, fps) / 100 * width;
            var isCentering = item.IsCentering;
            var interpolationMode = effectDescription.DrawDescription.ZoomInterpolationMode.ToTransform2D();


            Vector4 cropRect;
            Vector2 translation;

            if (top + bottom > height || left + right > width)
            {
                cropRect = new Vector4();
                translation = new Vector2();
            }
            else
            {
                cropRect = new Vector4(bounds.Left + left, bounds.Top + top, bounds.Right - right, bounds.Bottom - bottom);
                translation = isCentering ? new Vector2(right - left, bottom - top) / 2 : new Vector2();
            }


            if (isFirst || this.cropRect != cropRect)
            {
                crop.Rectangle = cropRect;
                this.cropRect = cropRect;
            }
            if (isFirst || this.translation != translation)
            {
                transform.TransformMatrix = Matrix3x2.CreateTranslation(translation);
                this.translation = translation;
            }
            if (isFirst || this.interpolationMode != interpolationMode)
            {
                transform.InterPolationMode = interpolationMode;
                this.interpolationMode = interpolationMode;
            }

            isFirst = false;
            return effectDescription.DrawDescription;
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            crop.SetInput(0, input, true);
        }

        public void ClearInput()
        {
            crop.SetInput(0, null, true);
            transform.SetInput(0, null, true);
        }

        public void Dispose()
        {
            disposer.Dispose();
        }  
    }
}