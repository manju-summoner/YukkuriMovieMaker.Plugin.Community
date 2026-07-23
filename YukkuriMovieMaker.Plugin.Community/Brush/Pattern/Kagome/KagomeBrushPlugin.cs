using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Kagome
{
    internal class KagomeBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Kagome;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupJapanesePatternName;
        public int DefaultOrder => 420;

        public IBrushParameter CreateBrushParameter()
        {
            return new KagomeBrushParameter();
        }
    }
}
