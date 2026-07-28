using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.OpenFx
{
    /// <summary>
    /// OpenFX（OFX）プラグインをホストする場面切り替え。
    /// トランジションコンテキスト対応のOFXプラグインを種類一覧から選んで使う
    /// </summary>
    public sealed class OpenFxTransitionPlugin : ITransitionPlugin
    {
        public string Name => Texts.OpenFxTransitionName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.TransitionGroupEffectName;
        public int DefaultOrder => 420;

        public ITransitionParameter CreateTransitionParameter() => new OpenFxTransitionParameter();
    }
}
