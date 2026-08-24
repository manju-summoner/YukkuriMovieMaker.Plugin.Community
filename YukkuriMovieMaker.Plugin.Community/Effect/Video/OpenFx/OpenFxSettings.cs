using System.Collections.Immutable;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OpenFXホスティングの設定。追加のプラグイン検索フォルダーを保持する
    /// </summary>
    internal class OpenFxSettings : SettingsBase<OpenFxSettings>
    {
        public override SettingsCategory Category => SettingsCategory.VideoEffect;

        public override string Name => "OpenFX";

        public override bool HasSettingView => true;

        public override object SettingView => new OpenFxSettingsView();

        /// <summary>
        /// 標準フォルダーに加えてOFXプラグインを検索するフォルダー
        /// </summary>
        public string[] AdditionalPluginDirectories { get => additionalPluginDirectories; set => Set(ref additionalPluginDirectories, value); }
        string[] additionalPluginDirectories = [];

        /// <summary>
        /// GPU/CPUの両方へ対応するOpenFXプラグインでGPUレンダリングを優先するか。
        /// falseの場合は利用可能なCUDAバックエンドがあってもCPU経路だけを使用する。
        /// </summary>
        public bool UseGpuRendering { get => useGpuRendering; set => Set(ref useGpuRendering, value); }
        bool useGpuRendering = true;

        /// <summary>
        /// お気に入りに登録したOFXプラグインのID
        /// </summary>
        public ImmutableList<string> FavoritePluginIds { get => favoritePluginIds; set => Set(ref favoritePluginIds, value); }
        ImmutableList<string> favoritePluginIds = [];

        public override void Initialize()
        {
            additionalPluginDirectories ??= [];
            favoritePluginIds ??= [];

            // 本番ビルドでもサードパーティプラグインの不具合を切り分けられるよう、
            // ホスト側の診断ログをYMM4のログへ流す（スキャナー子プロセスでは設定しない）
            OfxHostLog.Sink = message => Log.Default.Write(message);

            // VST3と同様、起動時にバックグラウンドで一覧を用意しておく。
            // 以後の更新はセレクターの更新ボタン・設定画面の再スキャンで行う
            Task.Run(() =>
            {
                try
                {
                    OpenFxPluginScanner.GetEffectPlugins();
                }
                catch (System.Exception e)
                {
                    Log.Default.Write("OFXプラグインの起動時スキャンに失敗しました。", e);
                }
            });
        }
    }
}
