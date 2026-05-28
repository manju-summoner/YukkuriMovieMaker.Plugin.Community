using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    internal class SeigaihaBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Seigaiha;

        public IBrushParameter CreateBrushParameter()
        {
            return new SeigaihaBrushParameter();
        }
    }
}
