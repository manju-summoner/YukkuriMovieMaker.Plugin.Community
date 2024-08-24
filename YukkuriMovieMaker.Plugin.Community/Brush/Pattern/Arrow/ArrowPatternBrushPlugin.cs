using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow
{
    internal class ArrowPatternBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.ArrowPattern;

        public IBrushParameter CreateBrushParameter()
        {
            return new ArrowPatternBrushParameter();
        }
    }
}
