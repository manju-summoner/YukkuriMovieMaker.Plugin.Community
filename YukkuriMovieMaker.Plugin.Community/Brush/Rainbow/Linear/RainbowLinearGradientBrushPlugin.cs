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
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.BrushGroupGradientName;
        public int DefaultOrder => 220;

        public IBrushParameter CreateBrushParameter()
        {
            return new RainbowLinearGradientBrushParameter();
        }
    }
}
