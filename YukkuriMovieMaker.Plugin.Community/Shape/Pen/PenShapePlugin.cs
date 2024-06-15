using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapePlugin : IShapePlugin
    {
        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public string Name => Texts.Pen;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new PenShapeParameter();
        }
    }
}
