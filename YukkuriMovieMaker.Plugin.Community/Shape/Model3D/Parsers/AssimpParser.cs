using Assimp;
using System.IO;
using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class AssimpParser : IModelParser
{
    private const string DefaultEmbeddedTextureExtension = ".png";
    private const PostProcessSteps ImportSteps =
        PostProcessSteps.Triangulate |
        PostProcessSteps.GenerateNormals |
        PostProcessSteps.FlipUVs |
        PostProcessSteps.CalculateTangentSpace |
        PostProcessSteps.MakeLeftHanded |
        PostProcessSteps.FlipWindingOrder |
        PostProcessSteps.GlobalScale |
        PostProcessSteps.ValidateDataStructure;

    private static readonly TextureType[] TextureTypePriority =
    [
        TextureType.Diffuse,
        TextureType.Unknown,
        TextureType.Emissive
    ];

    private static readonly string[] TextureSubDirectories =
    [
        "textures", "Textures", "images", "Images", "texture", "Texture", "tex", "Tex"
    ];

    private static readonly string[] TextureExtensionFallbacks =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tif", ".tiff", ".dds"
    ];

    private static readonly string[] FileExtensions =
    [
        ".3d", ".3ds", ".ac", ".ac3d", ".acc", ".ase", ".b3d", ".blend", ".bvh", ".cob", ".csm", ".dae", ".dxf",
        ".hmp", ".ifc", ".irr", ".irrmesh", ".lwo", ".lws", ".lxo", ".md2", ".md3", ".md5mesh", ".mdc", ".mdl",
        ".ms3d", ".ndo", ".nff", ".off", ".ogex", ".pk3", ".q3d", ".q3s", ".raw", ".scn", ".smd", ".ter", ".vta",
        ".x", ".xgl", ".zgl", ".fbx"
    ];

    public string Id => "Assimp";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        try
        {
            using var context = new AssimpContext();
            var scene = context.ImportFile(path, ImportSteps);
            if (scene is null || !scene.HasMeshes) return new Model3DData();

            var vertices = new List<Model3DVertex>();
            var indices = new List<int>();
            var parts = new List<Model3DPart>();

            ProcessNode(scene.RootNode, Matrix4x4.Identity, scene, vertices, indices, parts, path);

            var vertexArray = vertices.ToArray();
            ModelHelper.CalculateBounds(vertexArray, out var center, out float scale);

            return new Model3DData
            {
                Vertices = vertexArray,
                Indices = indices.ToArray(),
                Parts = parts,
                ModelCenter = center,
                ModelScale = scale
            };
        }
        catch
        {
            return new Model3DData();
        }
    }

    private static void ProcessNode(Node node, Matrix4x4 parentTransform, Scene scene, List<Model3DVertex> vertices, List<int> indices, List<Model3DPart> parts, string modelPath)
    {
        var worldTransform = ToNumerics(node.Transform) * parentTransform;

        if (node.HasMeshes)
        {
            foreach (var meshIndex in node.MeshIndices)
                ProcessMesh(scene.Meshes[meshIndex], worldTransform, scene, vertices, indices, parts, modelPath);
        }

        foreach (var child in node.Children)
            ProcessNode(child, worldTransform, scene, vertices, indices, parts, modelPath);
    }

    private static void ProcessMesh(Mesh mesh, Matrix4x4 transform, Scene scene, List<Model3DVertex> vertices, List<int> indices, List<Model3DPart> parts, string modelPath)
    {
        int vertexOffset = vertices.Count;
        int uvChannel = FindFirstTextureCoordinateChannel(mesh);

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var rawPosition = mesh.Vertices[i];
            var rawNormal = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 0, 0);
            var rawTexCoord = uvChannel >= 0 ? mesh.TextureCoordinateChannels[uvChannel][i] : new Vector3D(0, 0, 0);
            var rawColor = mesh.HasVertexColors(0) ? mesh.VertexColorChannels[0][i] : new Color4D(1, 1, 1, 1);

            vertices.Add(new Model3DVertex
            {
                Position = Vector3.Transform(new Vector3(rawPosition.X, rawPosition.Y, rawPosition.Z), transform),
                Normal = Vector3.TransformNormal(new Vector3(rawNormal.X, rawNormal.Y, rawNormal.Z), transform),
                TexCoord = new Vector2(rawTexCoord.X, rawTexCoord.Y),
                Color = new Vector4(rawColor.R, rawColor.G, rawColor.B, rawColor.A)
            });
        }

        int indexOffset = indices.Count;
        foreach (var index in mesh.GetIndices())
            indices.Add(index + vertexOffset);

        var part = new Model3DPart
        {
            IndexOffset = indexOffset,
            IndexCount = indices.Count - indexOffset
        };

        if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
        {
            var material = scene.Materials[mesh.MaterialIndex];

            if (material.HasColorDiffuse)
            {
                var diffuse = material.ColorDiffuse;
                part.BaseColor = new Vector4(diffuse.R, diffuse.G, diffuse.B, diffuse.A);
            }

            part.TexturePath = FindTexturePath(material, scene, modelPath);
        }

        parts.Add(part);
    }

    private static int FindFirstTextureCoordinateChannel(Mesh mesh)
    {
        for (int channel = 0; channel < mesh.TextureCoordinateChannelCount; channel++)
        {
            if (mesh.HasTextureCoords(channel)) return channel;
        }
        return -1;
    }

    private static string FindTexturePath(Material material, Scene scene, string modelPath)
    {
        foreach (var textureType in TextureTypePriority)
        {
            if (material.GetMaterialTextureCount(textureType) <= 0) continue;
            if (!material.GetMaterialTexture(textureType, 0, out var slot)) continue;

            string resolved = ResolveTextureSlot(slot, scene, modelPath);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        return string.Empty;
    }

    private static string ResolveTextureSlot(TextureSlot slot, Scene scene, string modelPath)
    {
        string rawPath = slot.FilePath;
        if (string.IsNullOrEmpty(rawPath)) return string.Empty;

        if (rawPath.StartsWith('*'))
            return ExtractEmbeddedTexture(rawPath, scene, modelPath);

        string modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        return FindExternalTexture(rawPath, modelDirectory);
    }

    private static string ExtractEmbeddedTexture(string rawPath, Scene scene, string modelPath)
    {
        if (!int.TryParse(rawPath.AsSpan(1), out int textureIndex)) return string.Empty;
        if ((uint)textureIndex >= (uint)scene.TextureCount) return string.Empty;

        var embedded = scene.Textures[textureIndex];
        if (!embedded.IsCompressed) return string.Empty;

        string extension = string.IsNullOrEmpty(embedded.CompressedFormatHint)
            ? DefaultEmbeddedTextureExtension
            : "." + embedded.CompressedFormatHint;

        return ModelHelper.WriteEmbeddedTexture(modelPath, textureIndex, extension, embedded.CompressedData);
    }

    private static string FindExternalTexture(string rawPath, string modelDirectory)
    {
        string cleanPath = rawPath;
        try
        {
            cleanPath = Uri.UnescapeDataString(rawPath);
        }
        catch
        {
        }

        if (cleanPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            cleanPath = cleanPath[7..];
            if (Path.DirectorySeparatorChar == '\\' && cleanPath.StartsWith('/') && cleanPath.Length > 2 && cleanPath[2] == ':')
                cleanPath = cleanPath[1..];
        }

        cleanPath = cleanPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        foreach (var candidate in EnumerateTextureCandidates(cleanPath, rawPath, modelDirectory))
        {
            if (File.Exists(candidate)) return candidate;
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateTextureCandidates(string cleanPath, string rawPath, string modelDirectory)
    {
        yield return cleanPath;
        if (rawPath != cleanPath) yield return rawPath;

        if (string.IsNullOrEmpty(modelDirectory)) yield break;

        yield return Path.Combine(modelDirectory, cleanPath);

        string fileName = Path.GetFileName(cleanPath);
        if (string.IsNullOrEmpty(fileName)) yield break;

        yield return Path.Combine(modelDirectory, fileName);

        foreach (var subDirectory in TextureSubDirectories)
            yield return Path.Combine(modelDirectory, subDirectory, fileName);

        string? parentDirectory = Directory.GetParent(modelDirectory)?.FullName;
        if (parentDirectory is not null)
        {
            yield return Path.Combine(parentDirectory, fileName);
            foreach (var subDirectory in TextureSubDirectories)
                yield return Path.Combine(parentDirectory, subDirectory, fileName);
        }

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        foreach (var extension in TextureExtensionFallbacks)
            yield return Path.Combine(modelDirectory, baseName + extension);
    }

    private static Matrix4x4 ToNumerics(Assimp.Matrix4x4 matrix) => new(
        matrix.A1, matrix.A2, matrix.A3, matrix.A4,
        matrix.B1, matrix.B2, matrix.B3, matrix.B4,
        matrix.C1, matrix.C2, matrix.C3, matrix.C4,
        matrix.D1, matrix.D2, matrix.D3, matrix.D4);
}
