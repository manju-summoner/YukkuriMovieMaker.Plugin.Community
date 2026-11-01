using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class FillStyleSettings : Bindable
    {
        public Color StrokeColor { get => strokeColor; set => Set(ref strokeColor, value); }
        Color strokeColor = Colors.White;

        public PenFillTolerance Tolerance { get => tolerance; set => Set(ref tolerance, value); }
        PenFillTolerance tolerance = PenFillTolerance.Medium;
    }
}
