using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

internal struct CullingBox
{
    public Vector3 Min;
    public Vector3 Max;

    public CullingBox()
    {
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(-float.MaxValue);
    }

    public CullingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public readonly bool IsEmpty => Min.X > Max.X;

    public void Expand(Vector3 point)
    {
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }
}
