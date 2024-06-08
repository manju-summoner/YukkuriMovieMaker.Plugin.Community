using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Radial
{
    internal class RainbowRadialGradientBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.RainbowRadialGradient;

        public IBrushParameter CreateBrushParameter()
        {
            return new RainbowRadialGradientBrushParameter();
        }
    }
}
