using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    public sealed class ReelSpinTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.ReelSpinTransitionPluginName;

        public ITransitionParameter CreateTransitionParameter() => new ReelSpinTransitionParameter();
    }
}
