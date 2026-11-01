namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenTaperExtensions
    {
        const double LowRate = 1;
        const double MediumRate = 2;
        const double HighRate = 4;

        public static double ToLength(this PenTaper taper, double thickness) => taper switch
        {
            PenTaper.Low => thickness * LowRate,
            PenTaper.Medium => thickness * MediumRate,
            PenTaper.High => thickness * HighRate,
            _ => 0,
        };
    }
}
