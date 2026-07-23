using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Asanoha
{
    internal class AsanohaBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Asanoha;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupJapanesePatternName;
        public int DefaultOrder => 430;

        public IBrushParameter CreateBrushParameter()
        {
            return new AsanohaBrushParameter();
        }
    }
}
