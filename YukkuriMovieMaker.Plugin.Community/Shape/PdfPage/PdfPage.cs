using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.PdfPage;

internal sealed class PdfPage : IShapePlugin
{
    public string Name => Texts.PdfPage;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.ShapeGroupFileName;
        public int DefaultOrder => 510;
    public bool IsExoShapeSupported => false;
    public bool IsExoMaskSupported => false;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        => new PdfPageParameter(sharedData);
}
