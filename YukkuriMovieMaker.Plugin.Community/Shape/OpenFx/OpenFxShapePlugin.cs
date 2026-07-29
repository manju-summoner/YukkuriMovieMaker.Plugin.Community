using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.OpenFx
{
    /// <summary>
    /// OpenFX（OFX）プラグインのジェネレーターコンテキストを図形としてホストするプラグイン。
    /// 入力なしで映像を生成するOFXプラグイン（グラデーション・チェッカーボード・ノイズ等）を
    /// 図形アイテム・図形切り抜きの図形として使えるようにする
    /// </summary>
    internal class OpenFxShapePlugin : IShapePlugin
    {
        public string Name => Texts.OpenFxShapeName;
        public string DefaultGroupName => YukkuriMovieMaker.Resources.Localization.Texts.ShapeGroupFileName;
        public int DefaultOrder => 530;
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new OpenFxShapeParameter(sharedData);
        }
    }
}
