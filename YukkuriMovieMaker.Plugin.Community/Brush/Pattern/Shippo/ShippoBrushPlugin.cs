using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Shippo
{
    internal class ShippoBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Shippo;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupJapanesePatternName;
        public int DefaultOrder => 400;

        public IBrushParameter CreateBrushParameter()
        {
            return new ShippoBrushParameter();
        }
    }
}
