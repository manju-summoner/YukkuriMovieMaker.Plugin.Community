using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenStyleSettings : Bindable
    {
        public Color StrokeColor { get => strokeColor; set => Set(ref strokeColor, value); }
        Color strokeColor = Colors.White;

        public double StrokeThickness { get => strokeThickness; set => Set(ref strokeThickness, Math.Max(0.1, value)); }
        double strokeThickness = 10;

        public bool IsPressure { get => isPressure; set => Set(ref isPressure, value); }
        bool isPressure = true;

        public PenStyleSettings()
        {

        }
    }
}
