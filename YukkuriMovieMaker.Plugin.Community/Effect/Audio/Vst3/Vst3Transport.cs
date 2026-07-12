namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal readonly record struct Vst3Transport(double Tempo, int TimeSignatureNumerator, int TimeSignatureDenominator, bool IsTempoValid);
}
