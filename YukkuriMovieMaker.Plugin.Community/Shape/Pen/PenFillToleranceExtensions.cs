namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenFillToleranceExtensions
    {
        const int LowDifference = 16;
        const int MediumDifference = 48;
        const int HighDifference = 96;

        public static int ToDifference(this PenFillTolerance tolerance) => tolerance switch
        {
            PenFillTolerance.Low => LowDifference,
            PenFillTolerance.Medium => MediumDifference,
            PenFillTolerance.High => HighDifference,
            _ => 0,
        };
    }
}
