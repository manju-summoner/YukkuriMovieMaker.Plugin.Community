using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel
{
    internal class ZoomPixelEffectProcessor : IVideoEffectProcessor
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly ZoomPixelEffect item;
        ID2D1Image? input;

        public ID2D1Image Output => input ?? throw new NullReferenceException(nameof(input) + "is null");

        public ZoomPixelEffectProcessor(IGraphicsDevicesAndContext devices, ZoomPixelEffect item)
        {
            this.devices = devices;
            this.item = item;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            float width = bounds.Right - bounds.Left;
            float height = bounds.Bottom - bounds.Top;

            return effectDescription.DrawDescription with
            {
                Zoom = item.Size.GetZoom(width, height, effectDescription),
                ZoomInterpolationMode = item.Dot ? InterpolationMode.NearestNeighbor : (InterpolationMode)Settings.YMMSettings.Default.GetZoomMode(),
            };
        }

        public void ClearInput()
        {
            input = null;
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
        }

        public void Dispose()
        {

        }
    }
}
