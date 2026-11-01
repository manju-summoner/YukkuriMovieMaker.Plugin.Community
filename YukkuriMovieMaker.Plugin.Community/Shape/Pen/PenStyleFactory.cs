using System.Windows.Ink;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenStyleFactory
    {
        public static DrawingAttributes CreatePen()
        {
            var size = PenSettings.Default.PenStyle.StrokeThickness;
            var color = PenSettings.Default.PenStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = size,
                IsHighlighter = false,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.PenStyle.IsPressure && PenSettings.Default.PenStyle.Taper is PenTaper.None,
                StylusTip = StylusTip.Ellipse,
            };
        }

        public static DrawingAttributes CreatePencil()
        {
            var size = PenSettings.Default.PencilStyle.StrokeThickness;
            var color = PenSettings.Default.PencilStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = size,
                IsHighlighter = false,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.PencilStyle.IsPressure && PenSettings.Default.PencilStyle.Taper is PenTaper.None,
                StylusTip = StylusTip.Ellipse,
            };
        }

        public static DrawingAttributes CreateFill()
        {
            return new DrawingAttributes
            {
                Color = PenSettings.Default.FillStyle.StrokeColor,
                IsHighlighter = false,
                FitToCurve = false,
                IgnorePressure = true,
                StylusTip = StylusTip.Ellipse,
            };
        }

        public static DrawingAttributes CreateHighlighter()
        {
            var size = PenSettings.Default.HighlighterStyle.StrokeThickness;
            var color = PenSettings.Default.HighlighterStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size / 2,
                Height = size,
                IsHighlighter = true,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.HighlighterStyle.IsPressure && PenSettings.Default.HighlighterStyle.Taper is PenTaper.None,
                StylusTip = StylusTip.Rectangle,
            };
        }
    }
}
