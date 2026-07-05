using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    internal class LensFlareShapePlugin : IShapePlugin
    {
        public string Name => Texts.ShapeTypeLensFlareName;
        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? store)
        {
            return new LensFlareShapeParameter(store!);
        }
    }
}
