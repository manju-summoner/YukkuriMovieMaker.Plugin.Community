using System.IO;
using System.Numerics;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class PmdParser : IStreamingModelParser
{
    private const string Signature = "Pmd";
    private const string InvalidDataMessage = "Invalid PMD data";
    private const int ShiftJisCodePage = 932;
    private const int ModelNameBytes = 20;
    private const int ModelCommentBytes = 256;
    private const int TexturePathBytes = 20;
    private const int VertexDeformBytes = 6;
    private const int MaterialShadingBlockBytes = 28;
    private const int MaterialFlagBlockBytes = 2;
    private const int VertexBytes = 38;
    private const int IndexBytes = 2;
    private const int MaterialBytes = 70;
    private const int StreamChunkLength = 4096;
    private const int StreamBufferBytes = 65536;

    private static readonly string[] FileExtensions = [".pmd"];
    private static readonly Encoding ShiftJis;

    static PmdParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(ShiftJisCodePage);
    }

    public string Id => "Pmd";
    public int Version => 2;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (!TryReadHeader(reader)) return new Model3DData();

        int vertexCount = reader.ReadInt32();
        if (!HasCapacity(stream, vertexCount, VertexBytes)) return new Model3DData();

        var vertices = GC.AllocateUninitializedArray<Model3DVertex>(vertexCount, true);
        for (int i = 0; i < vertexCount; i++)
            vertices[i] = ReadVertex(reader);

        int indexCount = reader.ReadInt32();
        if (!HasCapacity(stream, indexCount, IndexBytes)) return new Model3DData();

        var indices = GC.AllocateUninitializedArray<int>(indexCount, true);
        for (int i = 0; i < indexCount; i++)
            indices[i] = reader.ReadUInt16();

        var parts = ReadParts(reader, path);

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

            if (!TryReadHeader(reader)) throw new InvalidDataException(InvalidDataMessage);

            int vertexCount = reader.ReadInt32();
            if (!HasCapacity(stream, vertexCount, VertexBytes)) throw new InvalidDataException(InvalidDataMessage);

            var bounds = new CullingBox();
            using (var vertexTemp = CreateTempStream(vertexTempPath))
            {
                var chunk = new Model3DVertex[StreamChunkLength];
                int length = 0;

                for (int i = 0; i < vertexCount; i++)
                {
                    var vertex = ReadVertex(reader);
                    bounds.Expand(vertex.Position);
                    chunk[length++] = vertex;

                    if (length != StreamChunkLength) continue;
                    WriteVertices(vertexTemp, chunk, length);
                    length = 0;
                }

                if (length > 0) WriteVertices(vertexTemp, chunk, length);
            }

            int indexCount = reader.ReadInt32();
            if (!HasCapacity(stream, indexCount, IndexBytes)) throw new InvalidDataException(InvalidDataMessage);

            using (var indexTemp = CreateTempStream(indexTempPath))
            {
                var chunk = new int[StreamChunkLength];
                int length = 0;

                for (int i = 0; i < indexCount; i++)
                {
                    chunk[length++] = reader.ReadUInt16();

                    if (length != StreamChunkLength) continue;
                    WriteIndices(indexTemp, chunk, length);
                    length = 0;
                }

                if (length > 0) WriteIndices(indexTemp, chunk, length);
            }

            var parts = ReadParts(reader, path);
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

    private static bool TryReadHeader(BinaryReader reader)
    {
        var magic = reader.ReadBytes(Signature.Length);
        if (magic.Length != Signature.Length || Encoding.ASCII.GetString(magic) != Signature) return false;

        Skip(reader, sizeof(float));
        Skip(reader, ModelNameBytes);
        Skip(reader, ModelCommentBytes);
        return true;
    }

    private static Model3DVertex ReadVertex(BinaryReader reader)
    {
        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var texCoord = new Vector2(reader.ReadSingle(), reader.ReadSingle());

        Skip(reader, VertexDeformBytes);

        return new Model3DVertex
        {
            Position = position,
            Normal = normal,
            TexCoord = texCoord,
            Color = Vector4.One
        };
    }

    private static List<Model3DPart> ReadParts(BinaryReader reader, string modelPath)
    {
        int count = reader.ReadInt32();
        if (count <= 0) return [];
        if (!HasCapacity(reader.BaseStream, count, MaterialBytes)) throw new InvalidDataException(InvalidDataMessage);

        string modelDirectory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        var parts = new List<Model3DPart>(count);
        int indexOffset = 0;

        for (int i = 0; i < count; i++)
        {
            var diffuse = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Skip(reader, MaterialShadingBlockBytes);
            Skip(reader, MaterialFlagBlockBytes);

            int indexCount = reader.ReadInt32();
            if (indexCount < 0) throw new InvalidDataException(InvalidDataMessage);
            string rawPath = ReadFixedText(reader, TexturePathBytes);

            parts.Add(new Model3DPart
            {
                TexturePath = ResolveTexturePath(rawPath, modelDirectory),
                IndexOffset = indexOffset,
                IndexCount = indexCount,
                BaseColor = diffuse
            });

            indexOffset += indexCount;
        }

        return parts;
    }

    private static string ResolveTexturePath(string rawPath, string modelDirectory)
    {
        if (string.IsNullOrEmpty(rawPath)) return string.Empty;

        int sphereSeparator = rawPath.IndexOf('*');
        if (sphereSeparator >= 0) rawPath = rawPath[..sphereSeparator];

        return ModelHelper.ResolveTexturePath(rawPath, modelDirectory);
    }

    private static string ReadFixedText(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        int terminator = Array.IndexOf(bytes, (byte)0);
        return ShiftJis.GetString(bytes, 0, terminator >= 0 ? terminator : bytes.Length);
    }

    private static void Skip(BinaryReader reader, int bytes)
    {
        if (bytes > 0) reader.BaseStream.Seek(bytes, SeekOrigin.Current);
    }

    private static bool HasCapacity(Stream stream, int count, int elementBytes)
        => count >= 0 && (long)count * elementBytes <= stream.Length - stream.Position;
}
