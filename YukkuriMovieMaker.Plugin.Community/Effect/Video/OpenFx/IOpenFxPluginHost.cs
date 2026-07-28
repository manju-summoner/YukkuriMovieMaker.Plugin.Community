namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXプラグイン選択UI（<see cref="OpenFxPluginSelector"/>）の対象となるアイテム。
    /// 映像エフェクト（OpenFxVideoEffect）と場面切り替え（OpenFxTransitionParameter）が実装する
    /// </summary>
    internal interface IOpenFxPluginHost
    {
        /// <summary>プラグインバイナリ（.ofx）のパス</summary>
        string PluginPath { get; }

        /// <summary>プラグインの識別子（OfxPlugin.pluginIdentifier）</summary>
        string PluginId { get; }

        /// <summary>プラグインの表示名</summary>
        string PluginName { get; }

        /// <summary>プラグインを選択し、パラメータリストを再構築する（セレクターUIから呼ばれる）</summary>
        void SelectPlugin(OpenFxPluginInfo info);
    }
}
