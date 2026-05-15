namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal interface ILutParser
{
    bool CanParse(string filePath);
    CubeLut? Parse(string filePath);
}
