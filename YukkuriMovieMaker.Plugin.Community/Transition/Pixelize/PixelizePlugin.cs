using YukkuriMovieMaker.Plugin.Transition;

namespace YMM4SamplePlugin.Transition.Pixelize
{
    public sealed class PixelizePlugin : ITransitionPlugin
    {
        public string Name => Texts.PixelizeTransitionName;

        public ITransitionParameter CreateTransitionParameter() => new PixelizeParameter();
    }
}
