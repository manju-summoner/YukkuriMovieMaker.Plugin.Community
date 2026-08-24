using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    public sealed class PageTurnTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.PageTurnTransitionPluginName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.TransitionGroupEffectName;
        public int DefaultOrder => 400;

        public ITransitionParameter CreateTransitionParameter() => new PageTurnTransitionParameter();
    }
}
