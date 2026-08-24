using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.Pixelize
{
    public sealed class PixelizePlugin : ITransitionPlugin
    {
        public string Name => Texts.PixelizeTransitionName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.TransitionGroupEffectName;
        public int DefaultOrder => 410;

        public ITransitionParameter CreateTransitionParameter() => new PixelizeParameter();
    }
}
