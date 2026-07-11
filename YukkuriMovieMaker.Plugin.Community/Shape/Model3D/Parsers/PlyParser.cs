using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class PlyParser : IModelParser
{
    private static readonly string[] FileExtensions = [".ply"];

    public string Id => "Ply";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        if (!File.Exists(path)) return new Model3DData();

        Model3DData model;
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new PlyReader(stream);
            model = reader.Read();
        }
        catch
        {
            return new Model3DData();
        }

        if (model.Vertices.Length == 0) return new Model3DData();

        var dir = Path.GetDirectoryName(path);
        if (dir == null) return model;

        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            if (string.IsNullOrEmpty(part.TexturePath)) continue;

            part.TexturePath = Path.Combine(dir, part.TexturePath);
            model.Parts[i] = part;
        }

        return model;
    }

    private sealed class PlyReader
    {
        private readonly Stream _stream;
        private readonly BinaryReader _binReader;
        private bool _isBinary;
        private bool _isBigEndian;
        private int _vertexCount;
        private int _faceCount;
        private string _textureFile = "";
        private readonly List<PlyElement> _elements = [];
        private bool _indexLimitExceeded;

        public PlyReader(Stream stream)
        {
            _stream = stream;
            _binReader = new BinaryReader(stream);
        }

        public Model3DData Read()
        {
            if (!ParseHeader()) return new Model3DData();

            long remaining = _stream.Length - _stream.Position;
            if (_vertexCount < 0 || _faceCount < 0 || _vertexCount > remaining || _faceCount > remaining) return new Model3DData();

            var limits = Model3DSettings.Default;
            if (_vertexCount > limits.MaxVertices || (long)_faceCount * 3 > limits.MaxIndices) return new Model3DData();

            var vertices = new Model3DVertex[_vertexCount];
            var indices = new List<int>((int)Math.Min((long)_faceCount * 3, remaining));

            try
            {
                if (_isBinary) ReadBinaryData(vertices, indices);
                else ReadAsciiData(vertices, indices);
            }
            catch
            {
            }

            if (_indexLimitExceeded) return new Model3DData();

            int vLength = vertices.Length;
            if (indices.Count > 0 && vLength > 0)
            {
                var validIndices = new List<int>(indices.Count);
                for (int i = 0; i < indices.Count; i += 3)
                {
                    if (i + 2 >= indices.Count) break;
                    int i1 = indices[i];
                    int i2 = indices[i + 1];
                    int i3 = indices[i + 2];

                    if (i1 >= 0 && i1 < vLength && i2 >= 0 && i2 < vLength && i3 >= 0 && i3 < vLength)
                    {
                        validIndices.Add(i1);
                        validIndices.Add(i2);
                        validIndices.Add(i3);
                    }
                }
                indices = validIndices;
            }

            bool hasNormals = false;
            bool hasTransparentVertex = false;
            for (int i = 0; i < _vertexCount; i++)
            {
                if (vertices[i].Color.W < 0.001f) vertices[i].Color = new Vector4(vertices[i].Color.X, vertices[i].Color.Y, vertices[i].Color.Z, 1.0f);
                else if (vertices[i].Color.W < 1.0f) hasTransparentVertex = true;
                if (vertices[i].Normal.LengthSquared() > 0.001f) hasNormals = true;
            }

            if (!hasNormals && indices.Count > 0 && vLength > 0)
            {
                try
                {
                    ModelHelper.CalculateNormals(vertices, indices.ToArray());
                }
                catch { }
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 center, out float scale);

            var parts = new List<Model3DPart>
            {
                new Model3DPart
                {
                    TexturePath = _textureFile,
                    IndexCount = indices.Count,
                    ForceTransparent = hasTransparentVertex
                }
            };

            return new Model3DData
            {
                Vertices = vertices,
                Indices = indices.ToArray(),
                Parts = parts,
                ModelCenter = center,
                ModelScale = scale
            };
        }

        private bool ParseHeader()
        {
            _stream.Position = 0;
            string currentElement = "";

            while (true)
            {
                string? line = ReadLineFromStream();
                if (line == null) break;

                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line == "end_header") return true;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                if (parts[0] == "format")
                {
                    if (parts.Length >= 2)
                    {
                        if (parts[1].Contains("binary_little_endian")) _isBinary = true;
                        else if (parts[1].Contains("binary_big_endian")) { _isBinary = true; _isBigEndian = true; }
                    }
                }
                else if (parts[0] == "comment")
                {
                    if (parts.Length >= 3 && (parts[1] == "TextureFile" || parts[1] == "Texture"))
                        _textureFile = parts[2].Trim('"');
                }
                else if (parts[0] == "element")
                {
                    if (parts.Length >= 3)
                    {
                        currentElement = parts[1];
                        int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count);
                        _elements.Add(new PlyElement { Name = currentElement, Count = count });
                        if (currentElement == "vertex" && _vertexCount == 0) _vertexCount = count;
                        else if (currentElement == "face" && _faceCount == 0) _faceCount = count;
                    }
                }
                else if (parts[0] == "property")
                {
                    if (_elements.Count > 0) _elements[^1].Props.Add(ParseProperty(parts));
                }
            }
            return false;
        }

        private string? ReadLineFromStream()
        {
            var bytes = new List<byte>();
            int b;
            while ((b = _stream.ReadByte()) != -1)
            {
                if (b == '\n') break;
                bytes.Add((byte)b);
            }
            if (bytes.Count == 0 && b == -1) return null;
            return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
        }

        private PlyProperty ParseProperty(string[] parts)
        {
            string name = parts[parts.Length - 1];
            string normalizedName = NormalizePropertyName(name);

            if (parts.Length >= 4 && parts[1] == "list")
            {
                return new PlyProperty { Name = normalizedName, Type = PlyType.List, CountType = GetType(parts[2]), ItemType = GetType(parts[3]) };
            }
            return new PlyProperty { Name = normalizedName, Type = GetType(parts[1]) };
        }

        private static float NormalizeColorComponent(float value, PlyType type)
            => type is PlyType.Float or PlyType.Double
                ? Math.Clamp(value, 0.0f, 1.0f)
                : value / 255.0f;

        private static bool IsVertexIndexProperty(string name)
            => name is "vertex_indices" or "vertex_index";

        private static string NormalizePropertyName(string name)
        {
            name = name.ToLowerInvariant();
            if (name == "r" || name == "diffuse_red") return "red";
            if (name == "g" || name == "diffuse_green") return "green";
            if (name == "b" || name == "diffuse_blue") return "blue";
            if (name == "a" || name == "diffuse_alpha" || name == "opacity") return "alpha";
            if (name == "u" || name == "s" || name == "tx" || name == "texture_u") return "u";
            if (name == "v" || name == "t" || name == "ty" || name == "texture_v") return "v";
            return name;
        }

        private static PlyType GetType(string typeStr)
        {
            return typeStr switch
            {
                "char" or "int8" => PlyType.Char,
                "uchar" or "uint8" => PlyType.UChar,
                "short" or "int16" => PlyType.Short,
                "ushort" or "uint16" => PlyType.UShort,
                "int" or "int32" => PlyType.Int,
                "uint" or "uint32" => PlyType.UInt,
                "float" or "float32" => PlyType.Float,
                "double" or "float64" => PlyType.Double,
                _ => PlyType.Float,
            };
        }

        private void ReadAsciiData(Model3DVertex[] vertices, List<int> indices)
        {
            using var reader = new StreamReader(_stream, Encoding.ASCII, false, 65536, true);

            bool vertexDone = false, faceDone = false;
            foreach (var element in _elements)
            {
                if (element.Name == "vertex" && !vertexDone)
                {
                    vertexDone = true;
                    ReadAsciiVertices(reader, vertices, element);
                }
                else if (element.Name == "face" && !faceDone)
                {
                    faceDone = true;
                    ReadAsciiFaces(reader, indices, element);
                    if (_indexLimitExceeded) return;
                }
                else
                {
                    for (int skipped = 0; skipped < element.Count;)
                    {
                        var line = reader.ReadLine();
                        if (line == null) return;
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        skipped++;
                    }
                }
            }
        }

        private void ReadAsciiVertices(StreamReader reader, Model3DVertex[] vertices, PlyElement element)
        {
            int readV = 0;
            while (readV < element.Count && readV < vertices.Length)
            {
                var line = reader.ReadLine();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var span = line.AsSpan();
                    Vector3 pos = Vector3.Zero;
                    Vector3 norm = Vector3.Zero;
                    Vector2 uv = Vector2.Zero;
                    Vector4 col = Vector4.One;
                    float r = 1, g = 1, b = 1, a = 1;
                    bool hasColor = false;

                    foreach (var prop in element.Props)
                    {
                        span = TrimLeft(span);
                        int end = span.IndexOfAny(' ', '\t');
                        var valSpan = end == -1 ? span : span.Slice(0, end);
                        if (valSpan.Length > 0)
                        {
                            float val = FastParseFloat(valSpan);
                            switch (prop.Name)
                            {
                                case "x": pos.X = val; break;
                                case "y": pos.Y = val; break;
                                case "z": pos.Z = val; break;
                                case "nx": norm.X = val; break;
                                case "ny": norm.Y = val; break;
                                case "nz": norm.Z = val; break;
                                case "u": uv.X = val; break;
                                case "v": uv.Y = val; break;
                                case "red": r = NormalizeColorComponent(val, prop.Type); hasColor = true; break;
                                case "green": g = NormalizeColorComponent(val, prop.Type); hasColor = true; break;
                                case "blue": b = NormalizeColorComponent(val, prop.Type); hasColor = true; break;
                                case "alpha": a = NormalizeColorComponent(val, prop.Type); hasColor = true; break;
                            }
                        }
                        if (end == -1) break;
                        span = span.Slice(end + 1);
                    }
                    if (hasColor) col = new Vector4(r, g, b, a);
                    vertices[readV] = new Model3DVertex { Position = pos, Normal = norm, TexCoord = uv, Color = col };
                    readV++;
                }
                catch { }
            }
        }

        private void ReadAsciiFaces(StreamReader reader, List<int> indices, PlyElement element)
        {
            int maxIndices = Model3DSettings.Default.MaxIndices;
            int readF = 0;
            while (readF < element.Count)
            {
                var line = reader.ReadLine();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var span = line.AsSpan();
                    bool processed = false;
                    foreach (var prop in element.Props)
                    {
                        if (prop.Type == PlyType.List)
                        {
                            bool isIndexList = IsVertexIndexProperty(prop.Name);
                            span = TrimLeft(span);
                            int end = span.IndexOfAny(' ', '\t');
                            var countSpan = end == -1 ? span : span.Slice(0, end);
                            if (countSpan.Length > 0 && int.TryParse(countSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                            {
                                if (end != -1) span = span.Slice(end + 1);
                                int v0 = 0, vPrev = 0;
                                for (int k = 0; k < count; k++)
                                {
                                    span = TrimLeft(span);
                                    end = span.IndexOfAny(' ', '\t');
                                    var idxSpan = end == -1 ? span : span.Slice(0, end);
                                    if (isIndexList)
                                    {
                                        int vIdx = int.Parse(idxSpan, NumberStyles.Integer, CultureInfo.InvariantCulture);
                                        if (k == 0) v0 = vIdx;
                                        else if (k >= 2)
                                        {
                                            if (indices.Count + 3 > maxIndices) { _indexLimitExceeded = true; return; }
                                            indices.Add(v0); indices.Add(vPrev); indices.Add(vIdx);
                                        }
                                        vPrev = vIdx;
                                    }
                                    if (end == -1) break;
                                    span = span.Slice(end + 1);
                                }
                                if (isIndexList) processed = true;
                            }
                        }
                        else
                        {
                            span = TrimLeft(span);
                            int end = span.IndexOfAny(' ', '\t');
                            if (end != -1) span = span.Slice(end + 1);
                            else span = ReadOnlySpan<char>.Empty;
                        }
                    }
                    if (processed) readF++;
                }
                catch { }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> TrimLeft(ReadOnlySpan<char> span)
        {
            int start = 0;
            while (start < span.Length && char.IsWhiteSpace(span[start])) start++;
            return span.Slice(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastParseFloat(ReadOnlySpan<char> span)
        {
            float.TryParse(span, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float result);
            return result;
        }

        private void ReadBinaryData(Model3DVertex[] vertices, List<int> indices)
        {
            bool vertexDone = false, faceDone = false;
            foreach (var element in _elements)
            {
                if (element.Name == "vertex" && !vertexDone)
                {
                    vertexDone = true;
                    ReadBinaryVertices(vertices, element);
                }
                else if (element.Name == "face" && !faceDone)
                {
                    faceDone = true;
                    ReadBinaryFaces(indices, element);
                    if (_indexLimitExceeded) return;
                }
                else
                {
                    for (int i = 0; i < element.Count; i++)
                    {
                        foreach (var prop in element.Props)
                        {
                            if (prop.Type == PlyType.List)
                            {
                                int count = (int)ReadBinaryValue(prop.CountType);
                                for (int k = 0; k < count; k++) ReadBinaryValue(prop.ItemType);
                            }
                            else
                            {
                                ReadBinaryValue(prop.Type);
                            }
                        }
                    }
                }
            }
        }

        private void ReadBinaryVertices(Model3DVertex[] vertices, PlyElement element)
        {
            for (int i = 0; i < element.Count && i < vertices.Length; i++)
            {
                Vector3 pos = Vector3.Zero;
                Vector3 norm = Vector3.Zero;
                Vector2 uv = Vector2.Zero;
                Vector4 col = Vector4.One;
                float r = 1, g = 1, b = 1, a = 1;
                bool hasColor = false;

                foreach (var prop in element.Props)
                {
                    double val = ReadBinaryValue(prop.Type);
                    switch (prop.Name)
                    {
                        case "x": pos.X = (float)val; break;
                        case "y": pos.Y = (float)val; break;
                        case "z": pos.Z = (float)val; break;
                        case "nx": norm.X = (float)val; break;
                        case "ny": norm.Y = (float)val; break;
                        case "nz": norm.Z = (float)val; break;
                        case "u": uv.X = (float)val; break;
                        case "v": uv.Y = (float)val; break;
                        case "red": r = NormalizeColorComponent((float)val, prop.Type); hasColor = true; break;
                        case "green": g = NormalizeColorComponent((float)val, prop.Type); hasColor = true; break;
                        case "blue": b = NormalizeColorComponent((float)val, prop.Type); hasColor = true; break;
                        case "alpha": a = NormalizeColorComponent((float)val, prop.Type); hasColor = true; break;
                    }
                }
                if (hasColor) col = new Vector4(r, g, b, a);
                vertices[i] = new Model3DVertex { Position = pos, Normal = norm, TexCoord = uv, Color = col };
            }
        }

        private void ReadBinaryFaces(List<int> indices, PlyElement element)
        {
            int maxIndices = Model3DSettings.Default.MaxIndices;
            for (int i = 0; i < element.Count; i++)
            {
                foreach (var prop in element.Props)
                {
                    if (prop.Type == PlyType.List)
                    {
                        int count = (int)ReadBinaryValue(prop.CountType);
                        if (IsVertexIndexProperty(prop.Name))
                        {
                            int v0 = 0, vPrev = 0;
                            for (int k = 0; k < count; k++)
                            {
                                int vIdx = (int)ReadBinaryValue(prop.ItemType);
                                if (k == 0) v0 = vIdx;
                                else if (k >= 2)
                                {
                                    if (indices.Count + 3 > maxIndices) { _indexLimitExceeded = true; return; }
                                    indices.Add(v0); indices.Add(vPrev); indices.Add(vIdx);
                                }
                                vPrev = vIdx;
                            }
                        }
                        else
                        {
                            for (int k = 0; k < count; k++) ReadBinaryValue(prop.ItemType);
                        }
                    }
                    else
                    {
                        ReadBinaryValue(prop.Type);
                    }
                }
            }
        }

        private double ReadBinaryValue(PlyType type)
        {
            switch (type)
            {
                case PlyType.Char: return _binReader.ReadSByte();
                case PlyType.UChar: return _binReader.ReadByte();
                case PlyType.Short: return ReadInt16();
                case PlyType.UShort: return ReadUInt16();
                case PlyType.Int: return ReadInt32();
                case PlyType.UInt: return ReadUInt32();
                case PlyType.Float: return ReadSingle();
                case PlyType.Double: return ReadDouble();
                default: return 0;
            }
        }

        private short ReadInt16()
        {
            var val = _binReader.ReadInt16();
            return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
        }

        private ushort ReadUInt16()
        {
            var val = _binReader.ReadUInt16();
            return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
        }

        private int ReadInt32()
        {
            var val = _binReader.ReadInt32();
            return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
        }

        private uint ReadUInt32()
        {
            var val = _binReader.ReadUInt32();
            return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
        }

        private float ReadSingle()
        {
            var bytes = _binReader.ReadBytes(4);
            if (_isBigEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private double ReadDouble()
        {
            var bytes = _binReader.ReadBytes(8);
            if (_isBigEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        private enum PlyType { Char, UChar, Short, UShort, Int, UInt, Float, Double, List }

        private sealed class PlyProperty { public string Name = ""; public PlyType Type; public PlyType CountType; public PlyType ItemType; }

        private sealed class PlyElement { public string Name = ""; public int Count; public List<PlyProperty> Props { get; } = []; }
    }
}
