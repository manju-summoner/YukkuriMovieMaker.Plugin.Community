using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Scene
{
    internal class SceneBrushPlugin : IBrushPlugin
    {
        public string Name => Texts.Scene;

        public IBrushParameter CreateBrushParameter()
        {
            return new SceneBrushParameter();
        }
    }
}
