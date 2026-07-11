using System.IO;
using System.Numerics;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal static class ModelCacheFormat
{
    public const string SingleFileName = "model.bin";
    public const string SplitFilePattern = "part.*.bin";
    public const int SplitChunkSize = 256 * 1024;

    private const int MaxTexturePathLength = 32_767;
    private const int MaxDependencyCount = 4096;

    public static string GetSplitFileName(int index) => $"part.{index}.bin";

    public static void WriteHeader(BinaryWriter writer, CacheHeader header)
    {
        writer.Write(header.Signature);
        writer.Write(header.Version);
        writer.Write(header.Timestamp);
        writer.Write(header.OriginalPath);
        writer.Write(header.ParserId);
        writer.Write(header.ParserVersion);
        writer.Write(header.PluginVersion);
        writer.Write(header.FileHash);
    }

    public static CacheHeader ReadHeader(BinaryReader reader)
    {
        int signature = reader.ReadInt32();
        int version = reader.ReadInt32();
        long timestamp = reader.ReadInt64();
        string originalPath = reader.ReadString();
        string parserId = reader.ReadString();
        int parserVersion = reader.ReadInt32();
        string pluginVersion = reader.ReadString();
        string fileHash = reader.ReadString();

        return new CacheHeader(signature, version, timestamp, originalPath, parserId, parserVersion, pluginVersion, fileHash);
    }

    public static void WriteCounts(BinaryWriter writer, int vertexCount, int indexCount, int partCount)
    {
        writer.Write(vertexCount);
        writer.Write(indexCount);
        writer.Write(partCount);
    }

    public static void WritePart(BinaryWriter writer, Model3DPart part)
    {
        var textureBytes = Encoding.UTF8.GetBytes(part.TexturePath ?? string.Empty);
        writer.Write(textureBytes.Length);
        writer.Write(textureBytes);
        var metallicRoughnessBytes = Encoding.UTF8.GetBytes(part.MetallicRoughnessTexturePath ?? string.Empty);
        writer.Write(metallicRoughnessBytes.Length);
        writer.Write(metallicRoughnessBytes);
        writer.Write(part.IndexOffset);
        writer.Write(part.IndexCount);
        writer.Write(part.BaseColor.X);
        writer.Write(part.BaseColor.Y);
        writer.Write(part.BaseColor.Z);
        writer.Write(part.BaseColor.W);
        writer.Write(part.Metallic);
        writer.Write(part.Roughness);
        writer.Write(part.AlphaCutoff);
        writer.Write(part.ForceTransparent);
    }

    public static Model3DPart ReadPart(BinaryReader reader)
    {
        int textureLength = reader.ReadInt32();
        if (textureLength < 0 || textureLength > MaxTexturePathLength)
            throw new InvalidDataException($"Invalid texture path length: {textureLength}");

        string texturePath = Encoding.UTF8.GetString(reader.ReadBytes(textureLength));

        int metallicRoughnessLength = reader.ReadInt32();
        if (metallicRoughnessLength < 0 || metallicRoughnessLength > MaxTexturePathLength)
            throw new InvalidDataException($"Invalid texture path length: {metallicRoughnessLength}");

        string metallicRoughnessPath = Encoding.UTF8.GetString(reader.ReadBytes(metallicRoughnessLength));
        int indexOffset = reader.ReadInt32();
        int indexCount = reader.ReadInt32();
        if (indexOffset < 0 || indexCount < 0)
            throw new InvalidDataException($"Invalid part index range: {indexOffset}, {indexCount}");
        var baseColor = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        float metallic = reader.ReadSingle();
        float roughness = reader.ReadSingle();
        float alphaCutoff = reader.ReadSingle();
        bool forceTransparent = reader.ReadBoolean();

        return new Model3DPart
        {
            TexturePath = texturePath,
            MetallicRoughnessTexturePath = metallicRoughnessPath,
            IndexOffset = indexOffset,
            IndexCount = indexCount,
            BaseColor = baseColor,
            Metallic = metallic,
            Roughness = roughness,
            AlphaCutoff = alphaCutoff,
            ForceTransparent = forceTransparent
        };
    }

    public static void WriteDependencies(BinaryWriter writer, IReadOnlyList<string> dependencies)
    {
        int count = Math.Min(dependencies.Count, MaxDependencyCount);
        writer.Write(count);

        for (int i = 0; i < count; i++)
        {
            string path = dependencies[i] ?? string.Empty;
            writer.Write(path);
            writer.Write(GetDependencyTicks(path));
        }
    }

    public static List<(string Path, long Ticks)> ReadDependencies(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaxDependencyCount)
            throw new InvalidDataException($"Invalid dependency count: {count}");

        var dependencies = new List<(string, long)>(count);
        for (int i = 0; i < count; i++)
        {
            string path = reader.ReadString();
            long ticks = reader.ReadInt64();
            dependencies.Add((path, ticks));
        }
        return dependencies;
    }

    public static long GetDependencyTicks(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void WriteTransform(BinaryWriter writer, Vector3 center, float scale)
    {
        writer.Write(center.X);
        writer.Write(center.Y);
        writer.Write(center.Z);
        writer.Write(scale);
    }

    public static (Vector3 Center, float Scale) ReadTransform(BinaryReader reader)
    {
        var center = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        float scale = reader.ReadSingle();
        return (center, scale);
    }
}
