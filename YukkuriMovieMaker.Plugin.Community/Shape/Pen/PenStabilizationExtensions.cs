namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenStabilizationExtensions
    {
        const double LowStrength = 0.35;
        const double MediumStrength = 0.6;
        const double HighStrength = 0.82;

        public static double ToStrength(this PenStabilization stabilization) => stabilization switch
        {
            PenStabilization.Low => LowStrength,
            PenStabilization.Medium => MediumStrength,
            PenStabilization.High => HighStrength,
            _ => 0,
        };
    }
}
