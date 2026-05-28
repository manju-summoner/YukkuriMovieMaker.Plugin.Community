using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Shippo
{
    internal class ShippoBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Shippo;

        public IBrushParameter CreateBrushParameter()
        {
            return new ShippoBrushParameter();
        }
    }
}
