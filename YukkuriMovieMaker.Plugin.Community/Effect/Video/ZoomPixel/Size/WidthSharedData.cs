using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size
{
    internal class WidthSharedData
    {
        public Animation Width { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        public WidthSharedData()
        {
        }

        public WidthSharedData(IWidthParameter parameter)
        {
            Width.CopyFrom(parameter.Width);
        }

        public void CopyTo(IWidthParameter parameter)
        {
            parameter.Width.CopyFrom(Width);
        }
    }
}
