using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    internal class LensFlareShapePlugin : IShapePlugin
    {
        public string Name => Texts.ShapeTypeLensFlareName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.ShapeGroupEffectName;
        public int DefaultOrder => 310;
        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? store)
        {
            return new LensFlareShapeParameter(store!);
        }
    }
}
