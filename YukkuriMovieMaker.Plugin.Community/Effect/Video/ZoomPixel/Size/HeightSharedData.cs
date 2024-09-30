using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size
{
    internal class HeightSharedData
    {
        public Animation Height { get; } = new Animation(100, 0, 5000);

        public HeightSharedData()
        {
        }

        public HeightSharedData(IHeightParameter parameter)
        {
            Height.CopyFrom(parameter.Height);
        }

        public void CopyTo(IHeightParameter parameter)
        {
            parameter.Height.CopyFrom(Height);
        }
    }
}
