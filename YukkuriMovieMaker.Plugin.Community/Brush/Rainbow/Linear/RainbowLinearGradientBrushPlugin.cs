using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Linear
{
    internal class RainbowLinearGradientBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.RainbowLinearGradient;

        public IBrushParameter CreateBrushParameter()
        {
            return new RainbowLinearGradientBrushParameter();
        }
    }
}
