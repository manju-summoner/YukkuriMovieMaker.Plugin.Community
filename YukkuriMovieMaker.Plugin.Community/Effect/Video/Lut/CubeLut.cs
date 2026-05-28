namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

public sealed class CubeLut(
    string title,
    int size3D,
    float[] data3D,
    int size1D,
    float[]? data1D,
    (float R, float G, float B) domainMin,
    (float R, float G, float B) domainMax)
{
    public string Title { get; } = title;
    public int Size3D { get; } = size3D;
    public float[] Data3D { get; } = data3D;
    public int Size1D { get; } = size1D;
    public float[]? Data1D { get; } = data1D;
    public (float R, float G, float B) DomainMin { get; } = domainMin;
    public (float R, float G, float B) DomainMax { get; } = domainMax;

    public bool HasShaper => Size1D > 0 && Data1D is { Length: > 0 };

    public (float R, float G, float B) DomainScale => (
        DomainMax.R > DomainMin.R ? 1f / (DomainMax.R - DomainMin.R) : 1f,
        DomainMax.G > DomainMin.G ? 1f / (DomainMax.G - DomainMin.G) : 1f,
        DomainMax.B > DomainMin.B ? 1f / (DomainMax.B - DomainMin.B) : 1f);
}
