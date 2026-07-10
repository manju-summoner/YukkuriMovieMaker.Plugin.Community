using System.IO;
using System.Numerics;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class PmxParser : IStreamingModelParser
{
    private const string Signature = "PMX ";
    private const string InvalidDataMessage = "Invalid PMX data";
    private const int GlobalsLength = 8;
    private const int AdditionalUvStride = 16;
    private const int SdefExtraBytes = 36;
    private const int MaterialShadingBlockBytes = 28;
    private const int MaterialEdgeBlockBytes = 21;
    private const int MinVertexBytesWithoutBoneIndex = 37;
    private const int StreamChunkLength = 4096;
    private const int StreamBufferBytes = 65536;

    private static readonly string[] FileExtensions = [".pmx"];

    public string Id => "Pmx";
    public int Version => 3;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (!TryReadHeader(reader, out var header)) return new Model3DData();
        SkipModelInfo(reader);

        int vertexCount = reader.ReadInt32();
        if (!HasCapacity(stream, vertexCount, MinVertexBytesWithoutBoneIndex + header.BoneIndexSize)) return new Model3DData();

        var vertices = GC.AllocateUninitializedArray<Model3DVertex>(vertexCount, true);
        for (int i = 0; i < vertexCount; i++)
            vertices[i] = ReadVertex(reader, header);

        int indexCount = reader.ReadInt32();
        if (!HasCapacity(stream, indexCount, header.VertexIndexSize)) return new Model3DData();

        var indices = GC.AllocateUninitializedArray<int>(indexCount, true);
        for (int i = 0; i < indexCount; i++)
            indices[i] = ReadVertexIndex(reader, header.VertexIndexSize);

        var texturePaths = ReadTexturePaths(reader, header, path);
        var parts = ReadParts(reader, header, texturePaths);

        ModelHelper.CalculateBounds(vertices, out var center, out float scale);

        return new Model3DData
        {
            Vertices = vertices,
            Indices = indices,
            Parts = parts,
            ModelCenter = center,
            ModelScale = scale
        };
    }

    public Model3DData StreamToCache(string path, IStreamingCacheWriter cacheWriter)
    {
        string vertexTempPath = Path.GetTempFileName();
        string indexTempPath = Path.GetTempFileName();

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (!TryReadHeader(reader, out var header)) throw new InvalidDataException(InvalidDataMessage);
            SkipModelInfo(reader);

            int vertexCount = reader.ReadInt32();
            if (!HasCapacity(stream, vertexCount, MinVertexBytesWithoutBoneIndex + header.BoneIndexSize))
                throw new InvalidDataException(InvalidDataMessage);

            var bounds = new CullingBox();
            using (var vertexTemp = CreateTempStream(vertexTempPath))
            {
                var chunk = new Model3DVertex[StreamChunkLength];
                int length = 0;

                for (int i = 0; i < vertexCount; i++)
                {
                    var vertex = ReadVertex(reader, header);
                    bounds.Expand(vertex.Position);
                    chunk[length++] = vertex;

                    if (length != StreamChunkLength) continue;
                    WriteVertices(vertexTemp, chunk, length);
                    length = 0;
                }

                if (length > 0) WriteVertices(vertexTemp, chunk, length);
            }

            int indexCount = reader.ReadInt32();
            if (!HasCapacity(stream, indexCount, header.VertexIndexSize))
                throw new InvalidDataException(InvalidDataMessage);

            using (var indexTemp = CreateTempStream(indexTempPath))
            {
                var chunk = new int[StreamChunkLength];
                int length = 0;

                for (int i = 0; i < indexCount; i++)
                {
                    chunk[length++] = ReadVertexIndex(reader, header.VertexIndexSize);

                    if (length != StreamChunkLength) continue;
                    WriteIndices(indexTemp, chunk, length);
                    length = 0;
                }

                if (length > 0) WriteIndices(indexTemp, chunk, length);
            }

            var texturePaths = ReadTexturePaths(reader, header, path);
            var parts = ReadParts(reader, header, texturePaths);
            var (center, scale) = ModelHelper.CalculateTransform(bounds);

            cacheWriter.WriteMetadata(vertexCount, indexCount, parts, center, scale);
            ModelHelper.CopyToCache(vertexTempPath, cacheWriter.WriteVertexChunk);
            ModelHelper.CopyToCache(indexTempPath, cacheWriter.WriteIndexChunk);

            return new Model3DData
            {
                Parts = parts,
                ModelCenter = center,
                ModelScale = scale
            };
        }
        finally
        {
            ModelHelper.TryDeleteFile(vertexTempPath);
            ModelHelper.TryDeleteFile(indexTempPath);
        }
    }

    private static FileStream CreateTempStream(string path)
        => new(path, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferBytes);

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

    private static bool TryReadHeader(BinaryReader reader, out PmxHeader header)
    {
        header = default;

        var magic = reader.ReadBytes(Signature.Length);
        if (magic.Length != Signature.Length || Encoding.ASCII.GetString(magic) != Signature) return false;

        Skip(reader, sizeof(float));

        int globalsCount = reader.ReadByte();
        var globals = reader.ReadBytes(globalsCount);
        if (globals.Length < GlobalsLength)
        {
            var padded = new byte[GlobalsLength];
            globals.CopyTo(padded, 0);
            globals = padded;
        }

        header = new PmxHeader(
            globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8,
            globals[1],
            globals[2],
            globals[3],
            globals[5]);

        return IsValidIndexSize(header.VertexIndexSize)
            && IsValidIndexSize(header.TextureIndexSize)
            && IsValidIndexSize(header.BoneIndexSize);
    }

    private static bool IsValidIndexSize(int size) => size is 1 or 2 or 4;

    private static Model3DVertex ReadVertex(BinaryReader reader, in PmxHeader header)
    {
        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var texCoord = new Vector2(reader.ReadSingle(), reader.ReadSingle());

        Skip(reader, header.AdditionalUvCount * AdditionalUvStride);
        SkipDeform(reader, header.BoneIndexSize);
        Skip(reader, sizeof(float));

        return new Model3DVertex
        {
            Position = position,
            Normal = normal,
            TexCoord = texCoord,
            Color = Vector4.One
        };
    }

    private static void SkipDeform(BinaryReader reader, int boneIndexSize)
    {
        byte weightType = reader.ReadByte();
        int bytes = weightType switch
        {
            0 => boneIndexSize,
            1 => boneIndexSize * 2 + sizeof(float),
            2 or 4 => boneIndexSize * 4 + sizeof(float) * 4,
            3 => boneIndexSize * 2 + sizeof(float) + SdefExtraBytes,
            _ => 0
        };
        Skip(reader, bytes);
    }

    private static string[] ReadTexturePaths(BinaryReader reader, in PmxHeader header, string modelPath)
    {
        int count = reader.ReadInt32();
        if (count <= 0) return [];
        if (!HasCapacity(reader.BaseStream, count, sizeof(int))) throw new InvalidDataException(InvalidDataMessage);

        string modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        var paths = new string[count];

        for (int i = 0; i < count; i++)
        {
            string rawPath = ReadText(reader, header.Encoding);
            paths[i] = rawPath.Contains('*') ? string.Empty : ModelHelper.ResolveTexturePath(rawPath, modelDirectory);
        }

        return paths;
    }

    private static List<Model3DPart> ReadParts(BinaryReader reader, in PmxHeader header, string[] texturePaths)
    {
        int count = reader.ReadInt32();
        if (count <= 0) return [];
        if (!HasCapacity(reader.BaseStream, count, sizeof(int))) throw new InvalidDataException(InvalidDataMessage);

        var parts = new List<Model3DPart>(count);
        int indexOffset = 0;

        for (int i = 0; i < count; i++)
        {
            SkipLocalizedText(reader);

            var diffuse = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Skip(reader, MaterialShadingBlockBytes);
            Skip(reader, MaterialEdgeBlockBytes);

            int textureIndex = ReadSignedIndex(reader, header.TextureIndexSize);
            Skip(reader, header.TextureIndexSize);
            Skip(reader, sizeof(byte));

            byte isSharedToon = reader.ReadByte();
            Skip(reader, isSharedToon == 0 ? header.TextureIndexSize : sizeof(byte));
            SkipText(reader);

            int indexCount = reader.ReadInt32();

            parts.Add(new Model3DPart
            {
                TexturePath = (uint)textureIndex < (uint)texturePaths.Length ? texturePaths[textureIndex] : string.Empty,
                IndexOffset = indexOffset,
                IndexCount = indexCount,
                BaseColor = diffuse
            });

            indexOffset += indexCount;
        }

        return parts;
    }

    private static int ReadVertexIndex(BinaryReader reader, int size) => size switch
    {
        1 => reader.ReadByte(),
        2 => reader.ReadUInt16(),
        _ => reader.ReadInt32()
    };

    private static int ReadSignedIndex(BinaryReader reader, int size) => size switch
    {
        1 => reader.ReadSByte(),
        2 => reader.ReadInt16(),
        _ => reader.ReadInt32()
    };

    private static string ReadText(BinaryReader reader, Encoding encoding)
    {
        int length = reader.ReadInt32();
        if (length <= 0) return string.Empty;
        if (!HasCapacity(reader.BaseStream, length, sizeof(byte))) throw new InvalidDataException(InvalidDataMessage);
        return encoding.GetString(reader.ReadBytes(length)).Trim().Replace("\0", string.Empty);
    }

    private static void SkipModelInfo(BinaryReader reader)
    {
        SkipLocalizedText(reader);
        SkipLocalizedText(reader);
    }

    private static void SkipLocalizedText(BinaryReader reader)
    {
        SkipText(reader);
        SkipText(reader);
    }

    private static void SkipText(BinaryReader reader) => Skip(reader, reader.ReadInt32());

    private static void Skip(BinaryReader reader, int bytes)
    {
        if (bytes > 0) reader.BaseStream.Seek(bytes, SeekOrigin.Current);
    }

    private static bool HasCapacity(Stream stream, int count, int elementBytes)
        => count >= 0 && (long)count * elementBytes <= stream.Length - stream.Position;

    private readonly record struct PmxHeader(
        Encoding Encoding,
        int AdditionalUvCount,
        int VertexIndexSize,
        int TextureIndexSize,
        int BoneIndexSize);
}
