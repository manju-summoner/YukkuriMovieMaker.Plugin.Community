using YMM4SamplePlugin.Transition.Pixelize;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.Pixelize
{
    public sealed class PixelizePlugin : ITransitionPlugin
    {
        public string Name => Texts.PixelizeTransitionName;

        public ITransitionParameter CreateTransitionParameter() => new PixelizeParameter();
    }
}
