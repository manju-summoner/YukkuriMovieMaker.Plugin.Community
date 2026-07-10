using System.IO;
using System.Numerics;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal static class ModelCacheFormat
{
    private const int MaxTexturePathLength = 32_767;

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
        writer.Write(part.IndexOffset);
        writer.Write(part.IndexCount);
        writer.Write(part.BaseColor.X);
        writer.Write(part.BaseColor.Y);
        writer.Write(part.BaseColor.Z);
        writer.Write(part.BaseColor.W);
        writer.Write(part.Metallic);
        writer.Write(part.Roughness);
    }

    public static Model3DPart ReadPart(BinaryReader reader)
    {
        int textureLength = reader.ReadInt32();
        if (textureLength < 0 || textureLength > MaxTexturePathLength)
            throw new InvalidDataException($"Invalid texture path length: {textureLength}");

        string texturePath = Encoding.UTF8.GetString(reader.ReadBytes(textureLength));
        int indexOffset = reader.ReadInt32();
        int indexCount = reader.ReadInt32();
        var baseColor = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        float metallic = reader.ReadSingle();
        float roughness = reader.ReadSingle();

        return new Model3DPart
        {
            TexturePath = texturePath,
            IndexOffset = indexOffset,
            IndexCount = indexCount,
            BaseColor = baseColor,
            Metallic = metallic,
            Roughness = roughness
        };
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
