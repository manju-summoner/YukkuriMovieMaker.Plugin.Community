using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow2
{
    internal class ArrowPatternBrush2Plugin : IBrushPlugin
    {
        public string Name => Texts.ArrowPattern2;

        public IBrushParameter CreateBrushParameter()
        {
            return new ArrowPatternBrush2Parameter();
        }
    }
}
