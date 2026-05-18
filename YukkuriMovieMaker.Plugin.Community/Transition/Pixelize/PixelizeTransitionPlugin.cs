using YukkuriMovieMaker.Plugin.Transition;

namespace YMM4SamplePlugin.Transition.Pixelize
{
    public sealed class PixelizeTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.PixelizeTransitionName;

        public ITransitionParameter CreateTransitionParameter() => new PixelizeTransitionParameter();
    }
}
