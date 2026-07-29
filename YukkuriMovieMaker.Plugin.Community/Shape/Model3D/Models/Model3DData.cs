using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

internal sealed class Model3DData
{
    public const float NormalizedSize = 1.5f;

    public Model3DVertex[] Vertices { get; set; } = [];
    public int[] Indices { get; set; } = [];
    public List<Model3DPart> Parts { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public Vector3 ModelCenter { get; set; }
    public float ModelScale { get; set; } = 1.0f;
}
