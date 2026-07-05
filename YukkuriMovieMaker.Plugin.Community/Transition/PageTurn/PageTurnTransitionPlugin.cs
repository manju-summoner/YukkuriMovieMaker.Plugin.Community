using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    public sealed class PageTurnTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.PageTurnTransitionPluginName;

        public ITransitionParameter CreateTransitionParameter() => new PageTurnTransitionParameter();
    }
}
