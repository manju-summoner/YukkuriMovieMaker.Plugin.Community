using System.Numerics;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

[StructLayout(LayoutKind.Sequential)]
internal struct Model3DVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;
    public Vector4 Color;
}
