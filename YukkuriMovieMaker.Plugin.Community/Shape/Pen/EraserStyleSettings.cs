using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class EraserStyleSettings : Bindable
    {
        public double StrokeThickness { get => strokeThickness; set => Set(ref strokeThickness, Math.Max(0.1, value)); }
        double strokeThickness = 10;

        public EraserMode Mode { get => mode; set => Set(ref mode, value); }
        EraserMode mode = EraserMode.Line;
    }
}
