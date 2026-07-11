using System.IO;
using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal static class ModelHelper
{
    public const long MaxEmbeddedTextureBytes = 256L * 1024 * 1024;

    private const float MinimumExtent = 1e-6f;
    private const int StreamBufferBytes = 65536;
    private const int StreamChunkLength = 4096;
    private const string EmbeddedTextureDirectoryName = "YukkuriMovieMaker.Model3D";

    public static FileStream CreateTempStream(string path)
        => new(path, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferBytes);

    public static CullingBox WriteVertexStream(Stream stream, int vertexCount, Func<Model3DVertex> readVertex)
    {
        var bounds = new CullingBox();
        var chunk = new Model3DVertex[StreamChunkLength];
        int length = 0;

        for (int i = 0; i < vertexCount; i++)
        {
            var vertex = readVertex();
            bounds.Expand(vertex.Position);
            chunk[length++] = vertex;

            if (length != StreamChunkLength) continue;
            WriteVertices(stream, chunk, length);
            length = 0;
        }

        if (length > 0) WriteVertices(stream, chunk, length);
        return bounds;
    }

    public static void WriteIndexStream(Stream stream, int indexCount, Func<int> readIndex)
    {
        var chunk = new int[StreamChunkLength];
        int length = 0;

        for (int i = 0; i < indexCount; i++)
        {
            chunk[length++] = readIndex();

            if (length != StreamChunkLength) continue;
            WriteIndices(stream, chunk, length);
            length = 0;
        }

        if (length > 0) WriteIndices(stream, chunk, length);
    }

    private static unsafe void WriteVertices(Stream stream, Model3DVertex[] chunk, int length)
    {
        fixed (Model3DVertex* pointer = chunk)
            stream.Write(new ReadOnlySpan<byte>(pointer, length * sizeof(Model3DVertex)));
    }

    private static unsafe void WriteIndices(Stream stream, int[] chunk, int length)
    {
        fixed (int* pointer = chunk)
            stream.Write(new ReadOnlySpan<byte>(pointer, length * sizeof(int)));
    }

    public static void Skip(BinaryReader reader, int bytes)
    {
        if (bytes > 0) reader.BaseStream.Seek(bytes, SeekOrigin.Current);
    }

    public static bool HasCapacity(Stream stream, int count, int elementBytes)
        => count >= 0 && (long)count * elementBytes <= stream.Length - stream.Position;

    public static bool IsEmbeddedTexturePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), EmbeddedTextureDirectoryName));
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string WriteEmbeddedTexture(string modelPath, int index, string extension, byte[] data)
    {
        if (data.LongLength > MaxEmbeddedTextureBytes) return string.Empty;

        try
        {
            string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), EmbeddedTextureDirectoryName, ModelCache.ComputePathHash(modelPath)));
            Directory.CreateDirectory(directory);

            string filePath = Path.GetFullPath(Path.Combine(directory, index + extension));
            if (!filePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            File.WriteAllBytes(filePath, data);
            return filePath;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static (Vector3 Center, float Scale) CalculateTransform(CullingBox bounds)
    {
        if (bounds.IsEmpty) return (Vector3.Zero, 1.0f);

        var center = (bounds.Min + bounds.Max) * 0.5f;
        var size = bounds.Max - bounds.Min;
        float maxExtent = Math.Max(size.X, Math.Max(size.Y, size.Z));
        float scale = maxExtent > MinimumExtent ? Model3DData.NormalizedSize / maxExtent : 1.0f;

        return (center, scale);
    }

    public static string ResolveTexturePath(string rawPath, string modelDirectory)
        => ResolveTexturePath(rawPath, modelDirectory, modelDirectory);

    public static string ResolveTexturePath(string rawPath, string baseDirectory, string containmentRoot)
    {
        if (string.IsNullOrEmpty(rawPath)) return string.Empty;

        try
        {
            string normalized = rawPath.Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized)) return string.Empty;

            string baseFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(string.IsNullOrEmpty(baseDirectory) ? "." : baseDirectory));
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(string.IsNullOrEmpty(containmentRoot) ? "." : containmentRoot));
            string resolved = Path.GetFullPath(Path.Combine(baseFull, normalized));
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            return resolved;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void CopyToCache(string tempPath, CacheChunkWriter write)
    {
        using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferBytes, FileOptions.SequentialScan);
        var buffer = new byte[StreamBufferBytes];

        int read;
        while ((read = stream.Read(buffer)) > 0)
            write(buffer.AsSpan(0, read));
    }

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    public static unsafe void CalculateNormals(Model3DVertex[] vertices, int[] indices)
    {
        uint vertexCount = (uint)vertices.Length;

        fixed (Model3DVertex* pVerts = vertices)
        fixed (int* pInds = indices)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                pVerts[i].Normal = Vector3.Zero;
            }

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int i1 = pInds[i];
                int i2 = pInds[i + 1];
                int i3 = pInds[i + 2];
                if ((uint)i1 >= vertexCount || (uint)i2 >= vertexCount || (uint)i3 >= vertexCount) continue;

                Vector3 p1 = pVerts[i1].Position;
                Vector3 p2 = pVerts[i2].Position;
                Vector3 p3 = pVerts[i3].Position;

                Vector3 edge1 = p2 - p1;
                Vector3 edge2 = p3 - p1;
                Vector3 normal = Vector3.Cross(edge1, edge2);

                pVerts[i1].Normal += normal;
                pVerts[i2].Normal += normal;
                pVerts[i3].Normal += normal;
            }

            int len = vertices.Length;
            for (int i = 0; i < len; i++)
            {
                var n = pVerts[i].Normal;
                float lenSq = n.LengthSquared();
                if (lenSq > 1e-6f)
                {
                    pVerts[i].Normal = n / MathF.Sqrt(lenSq);
                }
            }
        }
    }

    public static void CalculateMissingNormals(Model3DVertex[] vertices, int[] indices)
    {
        uint vertexCount = (uint)vertices.Length;
        var missing = new bool[vertices.Length];
        bool anyMissing = false;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].Normal == Vector3.Zero)
            {
                missing[i] = true;
                anyMissing = true;
            }
        }

        if (!anyMissing) return;

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i1 = indices[i];
            int i2 = indices[i + 1];
            int i3 = indices[i + 2];
            if ((uint)i1 >= vertexCount || (uint)i2 >= vertexCount || (uint)i3 >= vertexCount) continue;
            if (!missing[i1] && !missing[i2] && !missing[i3]) continue;

            var normal = Vector3.Cross(
                vertices[i2].Position - vertices[i1].Position,
                vertices[i3].Position - vertices[i1].Position);

            if (missing[i1]) vertices[i1].Normal += normal;
            if (missing[i2]) vertices[i2].Normal += normal;
            if (missing[i3]) vertices[i3].Normal += normal;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!missing[i]) continue;

            var n = vertices[i].Normal;
            float lenSq = n.LengthSquared();
            if (lenSq > 1e-6f)
            {
                vertices[i].Normal = n / MathF.Sqrt(lenSq);
            }
        }
    }

    public static void CalculateBounds(Model3DVertex[] vertices, out Vector3 center, out float scale)
    {
        if (vertices.Length == 0)
        {
            center = Vector3.Zero;
            scale = 1.0f;
            return;
        }

        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(-float.MaxValue);

        if (Vector.IsHardwareAccelerated && vertices.Length >= Vector<float>.Count)
        {
            CalculateBoundsSimd(vertices, ref min, ref max);
        }
        else
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i].Position;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        (center, scale) = CalculateTransform(new CullingBox(min, max));
    }

    private static unsafe void CalculateBoundsSimd(Model3DVertex[] vertices, ref Vector3 min, ref Vector3 max)
    {
        var minX = new Vector<float>(float.MaxValue);
        var minY = new Vector<float>(float.MaxValue);
        var minZ = new Vector<float>(float.MaxValue);
        var maxX = new Vector<float>(-float.MaxValue);
        var maxY = new Vector<float>(-float.MaxValue);
        var maxZ = new Vector<float>(-float.MaxValue);

        int vecSize = Vector<float>.Count;
        int len = vertices.Length;
        int i = 0;

        float* xBuf = stackalloc float[vecSize];
        float* yBuf = stackalloc float[vecSize];
        float* zBuf = stackalloc float[vecSize];

        fixed (Model3DVertex* p = vertices)
        {
            byte* ptr = (byte*)p;
            int stride = sizeof(Model3DVertex);

            for (; i <= len - vecSize; i += vecSize)
            {
                for (int j = 0; j < vecSize; j++)
                {
                    var v = *(Model3DVertex*)(ptr + (i + j) * stride);
                    xBuf[j] = v.Position.X;
                    yBuf[j] = v.Position.Y;
                    zBuf[j] = v.Position.Z;
                }

                var vx = new Vector<float>(new Span<float>(xBuf, vecSize));
                var vy = new Vector<float>(new Span<float>(yBuf, vecSize));
                var vz = new Vector<float>(new Span<float>(zBuf, vecSize));

                minX = Vector.Min(minX, vx);
                minY = Vector.Min(minY, vy);
                minZ = Vector.Min(minZ, vz);
                maxX = Vector.Max(maxX, vx);
                maxY = Vector.Max(maxY, vy);
                maxZ = Vector.Max(maxZ, vz);
            }
        }

        for (int k = 0; k < vecSize; k++)
        {
            if (minX[k] < min.X) min.X = minX[k];
            if (minY[k] < min.Y) min.Y = minY[k];
            if (minZ[k] < min.Z) min.Z = minZ[k];
            if (maxX[k] > max.X) max.X = maxX[k];
            if (maxY[k] > max.Y) max.Y = maxY[k];
            if (maxZ[k] > max.Z) max.Z = maxZ[k];
        }

        for (; i < len; i++)
        {
            var p = vertices[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }
}
