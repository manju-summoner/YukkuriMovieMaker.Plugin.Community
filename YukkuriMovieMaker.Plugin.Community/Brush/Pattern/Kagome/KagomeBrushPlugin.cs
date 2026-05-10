using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Kagome
{
    internal class KagomeBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Kagome;

        public IBrushParameter CreateBrushParameter()
        {
            return new KagomeBrushParameter();
        }
    }
}
