using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    internal class SeigaihaBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Seigaiha;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupJapanesePatternName;
        public int DefaultOrder => 410;

        public IBrushParameter CreateBrushParameter()
        {
            return new SeigaihaBrushParameter();
        }
    }
}
