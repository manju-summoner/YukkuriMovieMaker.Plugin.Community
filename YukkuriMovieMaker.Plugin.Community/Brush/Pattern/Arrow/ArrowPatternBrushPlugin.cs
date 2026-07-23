using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow
{
    internal class ArrowPatternBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.ArrowPattern;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupJapanesePatternName;
        public int DefaultOrder => 440;

        public IBrushParameter CreateBrushParameter()
        {
            return new ArrowPatternBrushParameter();
        }
    }
}
