using System;
using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal class VectorFieldWarpEditorSettings : SettingsBase<VectorFieldWarpEditorSettings>
    {
        public const double MinCanvasHeight = 80;
        public const double MaxCanvasHeight = 1200;

        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "VectorFieldWarpEditor";

        public override bool HasSettingView => false;

        public override object? SettingView => null;

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
