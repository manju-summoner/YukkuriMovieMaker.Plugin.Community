using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    public sealed class SpreadPageTurnTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.SpreadPageTurnTransitionPluginName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.TransitionGroupEffectName;
        public int DefaultOrder => 401;

        public ITransitionParameter CreateTransitionParameter() => new SpreadPageTurnTransitionParameter();
    }
}
