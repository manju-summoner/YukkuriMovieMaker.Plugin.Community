using Assimp;
using System.IO;
using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class AssimpParser : IModelParser
{
    private const string DefaultEmbeddedTextureExtension = ".png";
    private const long MaxEmbeddedTextureBytes = 512L * 1024 * 1024;
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
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ProcessNode(scene.RootNode, Matrix4x4.Identity, scene, vertices, indices, parts, path, dependencies);

            var vertexArray = vertices.ToArray();
            ModelHelper.CalculateBounds(vertexArray, out var center, out float scale);

            return new Model3DData
            {
                Vertices = vertexArray,
                Indices = indices.ToArray(),
                Parts = parts,
                Dependencies = [.. dependencies],
                ModelCenter = center,
                ModelScale = scale
            };
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            return new Model3DData();
        }
    }

    private static void ProcessNode(Node node, Matrix4x4 parentTransform, Scene scene, List<Model3DVertex> vertices, List<int> indices, List<Model3DPart> parts, string modelPath, HashSet<string> dependencies)
    {
        var worldTransform = ToNumerics(node.Transform) * parentTransform;

        if (node.HasMeshes)
        {
            foreach (var meshIndex in node.MeshIndices)
                ProcessMesh(scene.Meshes[meshIndex], worldTransform, scene, vertices, indices, parts, modelPath, dependencies);
        }

        foreach (var child in node.Children)
            ProcessNode(child, worldTransform, scene, vertices, indices, parts, modelPath, dependencies);
    }

    private static void ProcessMesh(Mesh mesh, Matrix4x4 transform, Scene scene, List<Model3DVertex> vertices, List<int> indices, List<Model3DPart> parts, string modelPath, HashSet<string> dependencies)
    {
        var limits = Model3DSettings.Default;
        if (parts.Count >= limits.MaxParts) throw new ModelLimitExceededException();
        if (mesh.VertexCount > limits.MaxVertices - vertices.Count) throw new ModelLimitExceededException();
        if ((long)mesh.FaceCount * 3 > limits.MaxIndices - indices.Count) throw new ModelLimitExceededException();

        int vertexOffset = vertices.Count;

        var part = new Model3DPart();
        int textureUvIndex = -1;
        if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
        {
            var material = scene.Materials[mesh.MaterialIndex];

            if (material.HasColorDiffuse)
            {
                var diffuse = material.ColorDiffuse;
                part.BaseColor = new Vector4(diffuse.R, diffuse.G, diffuse.B, diffuse.A);
            }

            part.TexturePath = FindTexturePath(material, scene, modelPath, dependencies, out textureUvIndex);
        }

        int uvChannel = textureUvIndex >= 0 && mesh.HasTextureCoords(textureUvIndex)
            ? textureUvIndex
            : FindFirstTextureCoordinateChannel(mesh);
        var normalTransform = Matrix4x4.Invert(transform, out var inverseTransform)
            ? Matrix4x4.Transpose(inverseTransform)
            : transform;

        bool hasTransparentVertexColor = false;
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var rawPosition = mesh.Vertices[i];
            var rawNormal = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 0, 0);
            var rawTexCoord = uvChannel >= 0 ? mesh.TextureCoordinateChannels[uvChannel][i] : new Vector3D(0, 0, 0);
            var rawColor = mesh.HasVertexColors(0) ? mesh.VertexColorChannels[0][i] : new Color4D(1, 1, 1, 1);
            if (rawColor.A < 1.0f) hasTransparentVertexColor = true;

            vertices.Add(new Model3DVertex
            {
                Position = Vector3.Transform(new Vector3(rawPosition.X, rawPosition.Y, rawPosition.Z), transform),
                Normal = Vector3.TransformNormal(new Vector3(rawNormal.X, rawNormal.Y, rawNormal.Z), normalTransform),
                TexCoord = new Vector2(rawTexCoord.X, rawTexCoord.Y),
                Color = new Vector4(rawColor.R, rawColor.G, rawColor.B, rawColor.A)
            });
        }

        int indexOffset = indices.Count;
        foreach (var index in mesh.GetIndices())
            indices.Add(index + vertexOffset);

        part.IndexOffset = indexOffset;
        part.IndexCount = indices.Count - indexOffset;
        part.ForceTransparent = hasTransparentVertexColor;

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

    private static string FindTexturePath(Material material, Scene scene, string modelPath, HashSet<string> dependencies, out int uvIndex)
    {
        uvIndex = -1;

        foreach (var textureType in TextureTypePriority)
        {
            if (material.GetMaterialTextureCount(textureType) <= 0) continue;
            if (!material.GetMaterialTexture(textureType, 0, out var slot)) continue;

            string resolved = ResolveTextureSlot(slot, scene, modelPath, dependencies);
            if (!string.IsNullOrEmpty(resolved))
            {
                uvIndex = slot.UVIndex;
                return resolved;
            }
        }

        return string.Empty;
    }

    private static string ResolveTextureSlot(TextureSlot slot, Scene scene, string modelPath, HashSet<string> dependencies)
    {
        string rawPath = slot.FilePath;
        if (string.IsNullOrEmpty(rawPath)) return string.Empty;

        if (rawPath.StartsWith('*'))
            return ExtractEmbeddedTexture(rawPath, scene, modelPath);

        string modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        return FindExternalTexture(rawPath, modelDirectory, dependencies);
    }

    private static string ExtractEmbeddedTexture(string rawPath, Scene scene, string modelPath)
    {
        if (!int.TryParse(rawPath.AsSpan(1), out int textureIndex)) return string.Empty;
        if ((uint)textureIndex >= (uint)scene.TextureCount) return string.Empty;

        var embedded = scene.Textures[textureIndex];
        if (!embedded.IsCompressed) return WriteUncompressedEmbeddedTexture(embedded, textureIndex, modelPath);

        string extension = string.IsNullOrEmpty(embedded.CompressedFormatHint)
            ? DefaultEmbeddedTextureExtension
            : "." + embedded.CompressedFormatHint;

        return ModelHelper.WriteEmbeddedTexture(modelPath, textureIndex, extension, embedded.CompressedData);
    }

    private static string WriteUncompressedEmbeddedTexture(EmbeddedTexture embedded, int textureIndex, string modelPath)
    {
        try
        {
            int width = embedded.Width;
            int height = embedded.Height;
            var texels = embedded.NonCompressedData;
            if (width <= 0 || height <= 0 || texels == null) return string.Empty;
            if ((long)width * height * 4 > MaxEmbeddedTextureBytes || texels.Length < (long)width * height) return string.Empty;

            var pixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                var texel = texels[i];
                int offset = i * 4;
                pixels[offset] = texel.B;
                pixels[offset + 1] = texel.G;
                pixels[offset + 2] = texel.R;
                pixels[offset + 3] = texel.A;
            }

            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, width * 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return ModelHelper.WriteEmbeddedTexture(modelPath, textureIndex, ".png", stream.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindExternalTexture(string rawPath, string modelDirectory, HashSet<string> dependencies)
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

        if (string.IsNullOrEmpty(modelDirectory)) return string.Empty;

        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modelDirectory));
        }
        catch
        {
            return string.Empty;
        }

        string fallback = string.Empty;
        var missingCandidates = new List<string>();
        foreach (var candidate in EnumerateTextureCandidates(cleanPath, modelDirectory))
        {
            try
            {
                string full = Path.GetFullPath(candidate);
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(full)) return full;
                if (fallback.Length == 0) fallback = full;
                missingCandidates.Add(full);
            }
            catch
            {
            }
        }

        foreach (var candidate in missingCandidates)
        {
            dependencies.Add(candidate);
        }

        return fallback;
    }

    private static IEnumerable<string> EnumerateTextureCandidates(string cleanPath, string modelDirectory)
    {
        yield return Path.Combine(modelDirectory, cleanPath);

        string fileName = Path.GetFileName(cleanPath);
        if (string.IsNullOrEmpty(fileName)) yield break;

        yield return Path.Combine(modelDirectory, fileName);

        foreach (var subDirectory in TextureSubDirectories)
            yield return Path.Combine(modelDirectory, subDirectory, fileName);

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
