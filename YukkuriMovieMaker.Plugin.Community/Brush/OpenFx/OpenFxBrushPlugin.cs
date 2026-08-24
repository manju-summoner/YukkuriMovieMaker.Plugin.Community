using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.OpenFx
{
    /// <summary>
    /// OpenFX（OFX）プラグインのジェネレーターコンテキストをブラシとしてホストするプラグイン。
    /// 入力なしで映像を生成するOFXプラグイン（グラデーション・チェッカーボード・ノイズ等）を
    /// 図形やテキストの塗りつぶしブラシとして使えるようにする
    /// </summary>
    internal class OpenFxBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.OpenFxBrushName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupBasicName;
        public int DefaultOrder => 130;

        public IBrushParameter CreateBrushParameter()
        {
            return new OpenFxBrushParameter();
        }
    }
}
