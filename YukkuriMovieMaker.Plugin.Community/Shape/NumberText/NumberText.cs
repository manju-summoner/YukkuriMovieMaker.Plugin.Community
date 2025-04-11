using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.NumberText;

internal class NumberText : IShapePlugin
{
    public string Name => Texts.Number;
    public bool IsExoShapeSupported => false;
    public bool IsExoMaskSupported => false;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
    {
        return new NumberTextParameter(sharedData);
    }
}