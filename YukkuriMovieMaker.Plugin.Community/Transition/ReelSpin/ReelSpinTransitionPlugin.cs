using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    public sealed class ReelSpinTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.ReelSpinTransitionPluginName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.TransitionGroupSlideName;
        public int DefaultOrder => 230;

        public ITransitionParameter CreateTransitionParameter() => new ReelSpinTransitionParameter();
    }
}
