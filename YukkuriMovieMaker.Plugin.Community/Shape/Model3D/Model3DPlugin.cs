using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal sealed class Model3DPlugin : IShapePlugin
{
    public string Name => Texts.Model3D;
    public bool IsExoShapeSupported => false;
    public bool IsExoMaskSupported => false;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        => new Model3DParameter(sharedData);
}
