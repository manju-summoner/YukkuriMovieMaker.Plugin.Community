using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Asanoha
{
    internal class AsanohaBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Asanoha;

        public IBrushParameter CreateBrushParameter()
        {
            return new AsanohaBrushParameter();
        }
    }
}
