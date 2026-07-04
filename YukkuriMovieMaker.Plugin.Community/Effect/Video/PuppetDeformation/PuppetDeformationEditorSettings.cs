using System;
using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// パペット変形エディタのUI設定（プロジェクトに保存しない環境設定）。
    /// </summary>
    internal class PuppetDeformationEditorSettings : SettingsBase<PuppetDeformationEditorSettings>
    {
        public const double MinCanvasHeight = 80;
        public const double MaxCanvasHeight = 1200;

        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "PuppetDeformationEditor";

        public override bool HasSettingView => false;

        public override object? SettingView => null;

        /// <summary>ピン配置キャンバスの表示高さ</summary>
        public double CanvasHeight
        {
            get => canvasHeight;
            set => Set(ref canvasHeight, Math.Clamp(value, MinCanvasHeight, MaxCanvasHeight));
        }
        double canvasHeight = 240;

        public override void Initialize()
        {
        }
    }
}
