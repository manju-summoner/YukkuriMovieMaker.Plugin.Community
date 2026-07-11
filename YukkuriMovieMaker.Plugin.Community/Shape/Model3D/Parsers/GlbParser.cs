using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class GlbParser : IModelParser
{
    private const uint GlbMagic = 0x46546C67;
    private const uint JsonChunkType = 0x4E4F534A;
    private const uint BinChunkType = 0x004E4942;
    private const int ComponentTypeUByte = 5121;
    private const int ComponentTypeUShort = 5123;
    private const int ComponentTypeUInt = 5125;
    private const int ComponentTypeFloat = 5126;
    private const int ModeTriangles = 4;
    private const int ModeTriangleStrip = 5;
    private const int ModeTriangleFan = 6;

    private static readonly string[] FileExtensions = [".glb", ".gltf"];

    public string Id => "Glb";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        if (!File.Exists(path)) return new Model3DData();

        byte[]? binData = null;
        string jsonStr;

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadUInt32();
            if (magic == GlbMagic)
            {
                br.ReadUInt32();
                var length = br.ReadUInt32();

                if (fs.Position + 8 > length) return new Model3DData();
                var chunkLength = br.ReadInt32();
                var chunkType = br.ReadUInt32();

                if (chunkType != JsonChunkType) return new Model3DData();
                if (chunkLength < 0 || chunkLength > fs.Length - fs.Position) return new Model3DData();
                var jsonBytes = br.ReadBytes(chunkLength);
                jsonStr = Encoding.UTF8.GetString(jsonBytes);

                if (fs.Position < length)
                {
                    var binLength = br.ReadInt32();
                    var binType = br.ReadUInt32();
                    if (binType == BinChunkType && binLength >= 0 && binLength <= fs.Length - fs.Position)
                    {
                        binData = br.ReadBytes(binLength);
                    }
                }
            }
            else
            {
                fs.Position = 0;
                using var textReader = new StreamReader(fs, Encoding.UTF8);
                jsonStr = textReader.ReadToEnd();
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            return new Model3DData();
        }

        if (string.IsNullOrEmpty(jsonStr)) return new Model3DData();

        var allVertices = new List<Model3DVertex>();
        var allIndices = new List<int>();
        var parts = new List<Model3DPart>();
        var dependencies = new List<string>();
        var missingNormalRanges = new List<(int Start, int Count)>();

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("extensionsRequired", out var exts)
                && exts.ValueKind == JsonValueKind.Array
                && exts.GetArrayLength() > 0)
            {
                return new Model3DData();
            }

            var buffers = LoadBuffers(root, path, binData, dependencies);

            var images = new List<string>();
            if (root.TryGetProperty("images", out var imagesProp))
            {
                foreach (var img in imagesProp.EnumerateArray())
                {
                    string ext = ".png";
                    if (img.TryGetProperty("mimeType", out var mimeProp))
                    {
                        var mime = mimeProp.GetString();
                        if (mime == "image/jpeg") ext = ".jpg";
                    }

                    byte[]? imgBytes = null;
                    string externalPath = string.Empty;
                    if (img.TryGetProperty("bufferView", out var bvProp))
                    {
                        if (GetBufferViewInfo(root, bvProp.GetInt32(), out int bIdx, out int bOff, out int bLen, out _)
                            && GetBuffer(buffers, bIdx) is { } imgBuffer)
                        {
                            if (bOff >= 0 && bLen > 0 && bLen <= ModelHelper.MaxEmbeddedTextureBytes && (long)bOff + bLen <= imgBuffer.Length)
                            {
                                imgBytes = new byte[bLen];
                                Array.Copy(imgBuffer, bOff, imgBytes, 0, bLen);
                            }
                        }
                    }
                    else if (img.TryGetProperty("uri", out var uriProp))
                    {
                        var uri = uriProp.GetString();
                        if (!string.IsNullOrEmpty(uri))
                        {
                            if (uri.StartsWith("data:image", StringComparison.Ordinal))
                            {
                                int separator = uri.IndexOf(',');
                                if (separator >= 0 && (long)(uri.Length - separator - 1) / 4 * 3 <= ModelHelper.MaxEmbeddedTextureBytes)
                                {
                                    try
                                    {
                                        imgBytes = Convert.FromBase64String(uri[(separator + 1)..]);
                                    }
                                    catch (FormatException)
                                    {
                                        imgBytes = null;
                                    }
                                }
                            }
                            else if (!uri.StartsWith("data:", StringComparison.Ordinal))
                            {
                                externalPath = ResolveExternalUri(uri, path);
                                if (externalPath.Length > 0) dependencies.Add(externalPath);
                            }
                        }
                    }

                    images.Add(imgBytes != null
                        ? ModelHelper.WriteEmbeddedTexture(path, images.Count, ext, imgBytes)
                        : externalPath);
                }
            }

            var samplers = new List<(byte U, byte V)>();
            if (root.TryGetProperty("samplers", out var samplersProp) && samplersProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var sampler in samplersProp.EnumerateArray())
                {
                    byte u = sampler.TryGetProperty("wrapS", out var wrapS) ? MapWrapMode(wrapS.GetInt32()) : (byte)0;
                    byte v = sampler.TryGetProperty("wrapT", out var wrapT) ? MapWrapMode(wrapT.GetInt32()) : (byte)0;
                    samplers.Add((u, v));
                }
            }

            var textures = new List<(int Source, byte U, byte V)>();
            if (root.TryGetProperty("textures", out var texProp))
            {
                foreach (var tex in texProp.EnumerateArray())
                {
                    int source = tex.TryGetProperty("source", out var srcProp) ? srcProp.GetInt32() : -1;
                    byte u = 0, v = 0;
                    if (tex.TryGetProperty("sampler", out var samplerProp))
                    {
                        int samplerIdx = samplerProp.GetInt32();
                        if (samplerIdx >= 0 && samplerIdx < samplers.Count)
                        {
                            (u, v) = samplers[samplerIdx];
                        }
                    }
                    textures.Add((source, u, v));
                }
            }

            JsonElement nodes = default;
            if (root.TryGetProperty("nodes", out var nodesProp)) nodes = nodesProp;

            JsonElement meshes = default;
            if (root.TryGetProperty("meshes", out var meshesProp)) meshes = meshesProp;

            JsonElement materials = default;
            if (root.TryGetProperty("materials", out var mateProp)) materials = mateProp;

            if (nodes.ValueKind != JsonValueKind.Array && meshes.ValueKind != JsonValueKind.Array) return new Model3DData();

            var sceneNodes = new List<int>();
            bool sceneResolved = false;
            if (root.TryGetProperty("scenes", out var scenes) && scenes.ValueKind == JsonValueKind.Array && scenes.GetArrayLength() > 0)
            {
                int idx = root.TryGetProperty("scene", out var defaultSceneIdx) ? defaultSceneIdx.GetInt32() : 0;
                if (idx >= 0 && idx < scenes.GetArrayLength())
                {
                    sceneResolved = true;
                    var scene = scenes[idx];
                    if (scene.TryGetProperty("nodes", out var sceneNodeIds) && sceneNodeIds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var nodeVal in sceneNodeIds.EnumerateArray())
                        {
                            sceneNodes.Add(nodeVal.GetInt32());
                        }
                    }
                }
            }

            var context = new GlbContext(root, buffers, nodes, materials, images, textures, allVertices, allIndices, parts, missingNormalRanges);

            if (!sceneResolved && sceneNodes.Count == 0 && meshes.ValueKind == JsonValueKind.Array)
            {
                int meshCount = meshes.GetArrayLength();
                for (int i = 0; i < meshCount; i++)
                {
                    ProcessMesh(context, i, Matrix4x4.Identity);
                }
            }
            else
            {
                TraverseNodes(context, sceneNodes);
            }

            if (context.LimitExceeded) return new Model3DData();
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            return new Model3DData();
        }

        var verticesArr = allVertices.ToArray();
        var indicesArr = allIndices.ToArray();

        foreach (var (start, count) in missingNormalRanges)
        {
            CalculateNormalsRange(verticesArr, indicesArr, start, count);
        }

        ModelHelper.CalculateBounds(verticesArr, out Vector3 c, out float s);

        return new Model3DData
        {
            Vertices = verticesArr,
            Indices = indicesArr,
            Parts = parts,
            Dependencies = dependencies,
            ModelCenter = c,
            ModelScale = s
        };
    }

    private static void TraverseNodes(GlbContext context, List<int> rootNodes)
    {
        var nodes = context.Nodes;
        if (nodes.ValueKind != JsonValueKind.Array) return;

        int nodeCount = nodes.GetArrayLength();
        var visited = new HashSet<int>();
        var stack = new Stack<(int NodeIndex, Matrix4x4 ParentTransform)>();

        for (int i = rootNodes.Count - 1; i >= 0; i--)
        {
            stack.Push((rootNodes[i], Matrix4x4.Identity));
        }

        while (stack.Count > 0)
        {
            var (nodeIdx, parentTransform) = stack.Pop();
            if (nodeIdx < 0 || nodeIdx >= nodeCount || !visited.Add(nodeIdx)) continue;

            var node = nodes[nodeIdx];
            var worldTransform = GetLocalTransform(node) * parentTransform;

            if (node.TryGetProperty("mesh", out var meshIdxProp))
            {
                ProcessMesh(context, meshIdxProp.GetInt32(), worldTransform);
            }

            if (node.TryGetProperty("children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var childIdx in childrenProp.EnumerateArray())
                {
                    stack.Push((childIdx.GetInt32(), worldTransform));
                }
            }
        }
    }

    private static Matrix4x4 GetLocalTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matProp) && matProp.GetArrayLength() == 16)
        {
            var m = new float[16];
            int i = 0;
            foreach (var val in matProp.EnumerateArray()) m[i++] = val.GetSingle();
            return new Matrix4x4(
                m[0], m[1], m[2], m[3],
                m[4], m[5], m[6], m[7],
                m[8], m[9], m[10], m[11],
                m[12], m[13], m[14], m[15]
            );
        }

        var s = Vector3.One;
        var r = Quaternion.Identity;
        var t = Vector3.Zero;

        if (node.TryGetProperty("scale", out var sProp) && sProp.GetArrayLength() == 3)
        {
            s = new Vector3(sProp[0].GetSingle(), sProp[1].GetSingle(), sProp[2].GetSingle());
        }
        if (node.TryGetProperty("rotation", out var rProp) && rProp.GetArrayLength() == 4)
        {
            r = new Quaternion(rProp[0].GetSingle(), rProp[1].GetSingle(), rProp[2].GetSingle(), rProp[3].GetSingle());
        }
        if (node.TryGetProperty("translation", out var tProp) && tProp.GetArrayLength() == 3)
        {
            t = new Vector3(tProp[0].GetSingle(), tProp[1].GetSingle(), tProp[2].GetSingle());
        }

        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
    }

    private static void ProcessMesh(GlbContext context, int meshIdx, Matrix4x4 transform)
    {
        if (context.LimitExceeded) return;

        var (root, buffers, _, materials, images, textures, allVertices, allIndices, parts, missingNormalRanges) = context;
        if (!root.TryGetProperty("meshes", out var meshes) || meshIdx < 0 || meshIdx >= meshes.GetArrayLength()) return;

        var mesh = meshes[meshIdx];

        if (mesh.TryGetProperty("primitives", out var primitives))
        {
            var limits = Model3DSettings.Default;

            foreach (var prim in primitives.EnumerateArray())
            {
                if (allVertices.Count > limits.MaxVertices || allIndices.Count > limits.MaxIndices || parts.Count > limits.MaxParts)
                {
                    context.LimitExceeded = true;
                    return;
                }
                if (!prim.TryGetProperty("attributes", out var attrs)) continue;
                if (!attrs.TryGetProperty("POSITION", out var posAccIdxElem)) continue;

                int mode = prim.TryGetProperty("mode", out var modeElem) ? modeElem.GetInt32() : ModeTriangles;
                if (mode != ModeTriangles && mode != ModeTriangleStrip && mode != ModeTriangleFan) continue;

                int posAccIdx = posAccIdxElem.GetInt32();
                int normAccIdx = attrs.TryGetProperty("NORMAL", out var normElem) ? normElem.GetInt32() : -1;
                int colAccIdx = attrs.TryGetProperty("COLOR_0", out var colElem) ? colElem.GetInt32() : -1;
                int indAccIdx = prim.TryGetProperty("indices", out var indElem) ? indElem.GetInt32() : -1;
                int matIdx = prim.TryGetProperty("material", out var matElem) ? matElem.GetInt32() : -1;

                int texCoordSet = GetTextureTexCoordSet(materials, matIdx, "baseColorTexture");
                int mrTexCoordSet = GetTextureTexCoordSet(materials, matIdx, "metallicRoughnessTexture");
                int uvAccIdx = GetTexCoordAccessor(attrs, texCoordSet);
                int uv2AccIdx = mrTexCoordSet == texCoordSet ? uvAccIdx : GetTexCoordAccessor(attrs, mrTexCoordSet);

                if (IsSparseAccessor(root, posAccIdx) || IsSparseAccessor(root, normAccIdx) || IsSparseAccessor(root, uvAccIdx)
                    || IsSparseAccessor(root, uv2AccIdx) || IsSparseAccessor(root, colAccIdx) || IsSparseAccessor(root, indAccIdx)) continue;

                int posAccCount = GetAccessorCount(root, posAccIdx);
                if (posAccCount <= 0) continue;
                if (posAccCount > limits.MaxVertices - allVertices.Count)
                {
                    context.LimitExceeded = true;
                    return;
                }

                int srcAccCount = indAccIdx >= 0 ? GetAccessorCount(root, indAccIdx) : posAccCount;
                int expandedAccCount = mode == ModeTriangles ? srcAccCount : Math.Max(0, (srcAccCount - 2) * 3);
                if (expandedAccCount > limits.MaxIndices - allIndices.Count)
                {
                    context.LimitExceeded = true;
                    return;
                }

                var positions = ReadVector3Array(root, buffers, posAccIdx);
                if (positions == null || positions.Length == 0) continue;

                var normals = normAccIdx >= 0 ? ReadVector3Array(root, buffers, normAccIdx) : null;
                var uvs = uvAccIdx >= 0 ? ReadVector2Array(root, buffers, uvAccIdx) : null;
                var uv2s = uv2AccIdx == uvAccIdx ? uvs : (uv2AccIdx >= 0 ? ReadVector2Array(root, buffers, uv2AccIdx) : null);
                var colors = colAccIdx >= 0 ? ReadVector4Array(root, buffers, colAccIdx) : null;
                var indices = indAccIdx >= 0 ? ReadIntArray(root, buffers, indAccIdx) : null;

                int sourceIndexCount = indices?.Length ?? positions.Length;
                int indexAddCount = mode == ModeTriangles ? sourceIndexCount : Math.Max(0, (sourceIndexCount - 2) * 3);
                if (positions.Length > limits.MaxVertices - allVertices.Count || indexAddCount > limits.MaxIndices - allIndices.Count)
                {
                    context.LimitExceeded = true;
                    return;
                }

                var normalTransform = Matrix4x4.Invert(transform, out var inverseTransform)
                    ? Matrix4x4.Transpose(inverseTransform)
                    : transform;

                int vertexOffset = allVertices.Count;

                for (int i = 0; i < positions.Length; i++)
                {
                    var p = Vector3.Transform(positions[i], transform);
                    var n = Vector3.Zero;
                    if (normals != null && i < normals.Length)
                    {
                        n = Vector3.TransformNormal(normals[i], normalTransform);
                    }

                    var v = new Model3DVertex
                    {
                        Position = p,
                        Normal = n,
                        TexCoord = (uvs != null && i < uvs.Length) ? uvs[i] : Vector2.Zero,
                        TexCoord2 = (uv2s != null && i < uv2s.Length) ? uv2s[i] : Vector2.Zero,
                        Color = (colors != null && i < colors.Length) ? colors[i] : Vector4.One
                    };
                    allVertices.Add(v);
                }

                int startIndex = allIndices.Count;
                if (normals == null) missingNormalRanges.Add((startIndex, indexAddCount));
                if (mode == ModeTriangles)
                {
                    for (int i = 0; i < sourceIndexCount; i++)
                    {
                        allIndices.Add(GetSourceIndex(indices, i) + vertexOffset);
                    }
                }
                else
                {
                    for (int i = 2; i < sourceIndexCount; i++)
                    {
                        int a = mode == ModeTriangleFan ? 0 : ((i & 1) == 0 ? i - 2 : i - 1);
                        int b = mode == ModeTriangleFan ? i - 1 : ((i & 1) == 0 ? i - 1 : i - 2);
                        allIndices.Add(GetSourceIndex(indices, a) + vertexOffset);
                        allIndices.Add(GetSourceIndex(indices, b) + vertexOffset);
                        allIndices.Add(GetSourceIndex(indices, i) + vertexOffset);
                    }
                }

                Vector4 baseColor = Vector4.One;
                float metallic = Model3DPart.DefaultMetallic;
                float roughness = Model3DPart.DefaultRoughness;
                string texPath = string.Empty;
                string metallicRoughnessTexPath = string.Empty;

                bool forceTransparent = false;
                float alphaCutoff = 0.0f;
                byte addressU = 0;
                byte addressV = 0;

                if (colors != null)
                {
                    foreach (var vertexColor in colors)
                    {
                        if (vertexColor.W < 1.0f)
                        {
                            forceTransparent = true;
                            break;
                        }
                    }
                }

                if (matIdx >= 0 && materials.ValueKind == JsonValueKind.Array && matIdx < materials.GetArrayLength())
                {
                    var mat = materials[matIdx];

                    if (mat.TryGetProperty("alphaMode", out var alphaModeProp))
                    {
                        string? alphaMode = alphaModeProp.GetString();
                        if (alphaMode == "BLEND")
                        {
                            forceTransparent = true;
                        }
                        else if (alphaMode == "MASK")
                        {
                            alphaCutoff = mat.TryGetProperty("alphaCutoff", out var cutoffProp)
                                ? Math.Clamp(cutoffProp.GetSingle(), 0.0f, 1.0f)
                                : 0.5f;
                        }
                    }

                    if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr))
                    {
                        if (pbr.TryGetProperty("baseColorFactor", out var colFactor) && colFactor.GetArrayLength() == 4)
                        {
                            baseColor = new Vector4(
                                colFactor[0].GetSingle(),
                                colFactor[1].GetSingle(),
                                colFactor[2].GetSingle(),
                                colFactor[3].GetSingle()
                            );
                        }
                        if (pbr.TryGetProperty("metallicFactor", out var mProp)) metallic = mProp.GetSingle();
                        if (pbr.TryGetProperty("roughnessFactor", out var rProp)) roughness = rProp.GetSingle();

                        if (pbr.TryGetProperty("baseColorTexture", out var bTexProp))
                        {
                            if (bTexProp.TryGetProperty("index", out var bTexIdxProp))
                            {
                                int bTexIdx = bTexIdxProp.GetInt32();
                                if (bTexIdx >= 0 && bTexIdx < textures.Count)
                                {
                                    var texture = textures[bTexIdx];
                                    if (texture.Source >= 0 && texture.Source < images.Count)
                                    {
                                        texPath = images[texture.Source];
                                        addressU = texture.U;
                                        addressV = texture.V;
                                    }
                                }
                            }
                        }

                        if (pbr.TryGetProperty("metallicRoughnessTexture", out var mrTexProp)
                            && mrTexProp.TryGetProperty("index", out var mrTexIdxProp))
                        {
                            int mrTexIdx = mrTexIdxProp.GetInt32();
                            if (mrTexIdx >= 0 && mrTexIdx < textures.Count)
                            {
                                var texture = textures[mrTexIdx];
                                if (texture.Source >= 0 && texture.Source < images.Count)
                                {
                                    metallicRoughnessTexPath = images[texture.Source];
                                }
                            }
                        }
                    }
                }

                parts.Add(new Model3DPart
                {
                    TexturePath = texPath,
                    MetallicRoughnessTexturePath = metallicRoughnessTexPath,
                    IndexOffset = startIndex,
                    IndexCount = allIndices.Count - startIndex,
                    BaseColor = baseColor,
                    Metallic = metallic,
                    Roughness = roughness,
                    AlphaCutoff = alphaCutoff,
                    ForceTransparent = forceTransparent,
                    IgnoreAlpha = !forceTransparent && alphaCutoff <= 0.0f,
                    AddressU = addressU,
                    AddressV = addressV
                });
            }
        }
    }

    private static Vector3[]? ReadVector3Array(JsonElement root, byte[]?[] buffers, int accessorIdx)
    {
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out _)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out int viewLength, out int stride)) return null;
        if (GetBuffer(buffers, buffIdx) is not { } binData) return null;

        if (stride <= 0) stride = 12;
        if (!TryClampAccessorCount(binData, viewOffset, viewLength, offset, stride, 12, ref count, out int start)) return null;

        var result = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + 12 > binData.Length) break;

            float x = BitConverter.ToSingle(binData, p);
            float y = BitConverter.ToSingle(binData, p + 4);
            float z = BitConverter.ToSingle(binData, p + 8);
            result[i] = new Vector3(x, y, z);
        }
        return result;
    }

    private static Vector2[]? ReadVector2Array(JsonElement root, byte[]?[] buffers, int accessorIdx)
    {
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out int viewLength, out int stride)) return null;
        if (GetBuffer(buffers, buffIdx) is not { } binData) return null;

        int componentSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        int elementSize = componentSize * 2;
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, viewLength, offset, stride, elementSize, ref count, out int start)) return null;

        var result = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + elementSize > binData.Length) break;

            float x = 0, y = 0;

            if (compType == ComponentTypeFloat)
            {
                x = BitConverter.ToSingle(binData, p);
                y = BitConverter.ToSingle(binData, p + 4);
            }
            else if (compType == ComponentTypeUByte)
            {
                x = binData[p] / 255.0f;
                y = binData[p + 1] / 255.0f;
            }
            else if (compType == ComponentTypeUShort)
            {
                x = BitConverter.ToUInt16(binData, p) / 65535.0f;
                y = BitConverter.ToUInt16(binData, p + 2) / 65535.0f;
            }
            result[i] = new Vector2(x, y);
        }
        return result;
    }

    private static Vector4[]? ReadVector4Array(JsonElement root, byte[]?[] buffers, int accessorIdx)
    {
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType, out string type)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out int viewLength, out int stride)) return null;
        if (GetBuffer(buffers, buffIdx) is not { } binData) return null;

        int componentCount = type == "VEC3" ? 3 : 4;
        int componentSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        int elementSize = componentSize * componentCount;
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, viewLength, offset, stride, elementSize, ref count, out int start)) return null;

        var result = new Vector4[count];
        bool hasAlpha = componentCount == 4;

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + elementSize > binData.Length) break;

            float x = 0, y = 0, z = 0, w = 1;

            if (compType == ComponentTypeFloat)
            {
                x = BitConverter.ToSingle(binData, p);
                y = BitConverter.ToSingle(binData, p + 4);
                z = BitConverter.ToSingle(binData, p + 8);
                if (hasAlpha) w = BitConverter.ToSingle(binData, p + 12);
            }
            else if (compType == ComponentTypeUByte)
            {
                x = binData[p] / 255.0f;
                y = binData[p + 1] / 255.0f;
                z = binData[p + 2] / 255.0f;
                if (hasAlpha) w = binData[p + 3] / 255.0f;
            }
            else if (compType == ComponentTypeUShort)
            {
                x = BitConverter.ToUInt16(binData, p) / 65535.0f;
                y = BitConverter.ToUInt16(binData, p + 2) / 65535.0f;
                z = BitConverter.ToUInt16(binData, p + 4) / 65535.0f;
                if (hasAlpha) w = BitConverter.ToUInt16(binData, p + 6) / 65535.0f;
            }
            result[i] = new Vector4(x, y, z, w);
        }
        return result;
    }

    private static int[]? ReadIntArray(JsonElement root, byte[]?[] buffers, int accessorIdx)
    {
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out int viewLength, out int stride)) return null;
        if (GetBuffer(buffers, buffIdx) is not { } binData) return null;

        int elementSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, viewLength, offset, stride, elementSize, ref count, out int start)) return null;

        var result = new int[count];

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + elementSize > binData.Length) break;

            if (compType == ComponentTypeUByte)
            {
                result[i] = binData[p];
            }
            else if (compType == ComponentTypeUShort)
            {
                result[i] = BitConverter.ToUInt16(binData, p);
            }
            else if (compType == ComponentTypeUInt)
            {
                result[i] = (int)BitConverter.ToUInt32(binData, p);
            }
        }
        return result;
    }

    private static bool TryClampAccessorCount(byte[] binData, int viewOffset, int viewLength, int offset, int stride, int elementSize, ref int count, out int start)
    {
        start = 0;
        if (viewOffset < 0 || viewLength < 0 || offset < 0 || count < 0) return false;

        long longStart = (long)viewOffset + offset;
        long viewEnd = Math.Min((long)viewOffset + viewLength, binData.Length);
        long available = viewEnd - longStart;
        if (longStart > int.MaxValue || available < elementSize) return false;

        start = (int)longStart;
        long maxCount = (available - elementSize) / stride + 1;
        if (count > maxCount) count = (int)maxCount;
        return true;
    }

    private static void CalculateNormalsRange(Model3DVertex[] vertices, int[] indices, int start, int count)
    {
        var touched = new HashSet<int>();
        int end = (int)Math.Min((long)start + count, indices.Length);

        for (int i = start; i + 2 < end; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length) continue;

            var normal = Vector3.Cross(
                vertices[i1].Position - vertices[i0].Position,
                vertices[i2].Position - vertices[i0].Position);

            vertices[i0].Normal += normal;
            vertices[i1].Normal += normal;
            vertices[i2].Normal += normal;
            touched.Add(i0);
            touched.Add(i1);
            touched.Add(i2);
        }

        foreach (var vi in touched)
        {
            var normal = vertices[vi].Normal;
            if (normal.LengthSquared() > 1e-12f) vertices[vi].Normal = Vector3.Normalize(normal);
        }
    }

    private static int GetTextureTexCoordSet(JsonElement materials, int matIdx, string textureProperty)
    {
        if (matIdx < 0 || materials.ValueKind != JsonValueKind.Array || matIdx >= materials.GetArrayLength()) return 0;

        var mat = materials[matIdx];
        if (mat.TryGetProperty("pbrMetallicRoughness", out var pbr)
            && pbr.TryGetProperty(textureProperty, out var tex)
            && tex.TryGetProperty("texCoord", out var texCoordProp))
        {
            return Math.Max(0, texCoordProp.GetInt32());
        }
        return 0;
    }

    private static int GetTexCoordAccessor(JsonElement attrs, int texCoordSet)
    {
        if (attrs.TryGetProperty("TEXCOORD_" + texCoordSet.ToString(System.Globalization.CultureInfo.InvariantCulture), out var uvElem))
        {
            return uvElem.GetInt32();
        }
        return attrs.TryGetProperty("TEXCOORD_0", out var uv0Elem) ? uv0Elem.GetInt32() : -1;
    }

    private static byte MapWrapMode(int gltfWrapMode) => gltfWrapMode switch
    {
        33071 => 1,
        33648 => 2,
        _ => 0
    };

    private static bool IsSparseAccessor(JsonElement root, int index)
    {
        if (index < 0) return false;
        if (!root.TryGetProperty("accessors", out var accessors) || index >= accessors.GetArrayLength()) return false;
        return accessors[index].TryGetProperty("sparse", out _);
    }

    private static int GetAccessorCount(JsonElement root, int index)
    {
        if (!root.TryGetProperty("accessors", out var accessors) || index < 0 || index >= accessors.GetArrayLength()) return 0;
        return accessors[index].TryGetProperty("count", out var countProp) ? countProp.GetInt32() : 0;
    }

    private static bool GetAccessorInfo(JsonElement root, int index, out int buffView, out int offset, out int count, out int compType)
        => GetAccessorInfo(root, index, out buffView, out offset, out count, out compType, out _);

    private static bool GetAccessorInfo(JsonElement root, int index, out int buffView, out int offset, out int count, out int compType, out string type)
    {
        buffView = -1; offset = 0; count = 0; compType = 0; type = string.Empty;
        if (!root.TryGetProperty("accessors", out var accessors) || index < 0 || index >= accessors.GetArrayLength()) return false;

        var acc = accessors[index];
        if (acc.TryGetProperty("sparse", out _)) return false;
        if (acc.TryGetProperty("bufferView", out var bvElem)) buffView = bvElem.GetInt32();
        if (acc.TryGetProperty("byteOffset", out var offElem)) offset = offElem.GetInt32();
        if (acc.TryGetProperty("count", out var cntElem)) count = cntElem.GetInt32();
        if (acc.TryGetProperty("componentType", out var typeElem)) compType = typeElem.GetInt32();
        if (acc.TryGetProperty("type", out var accTypeElem)) type = accTypeElem.GetString() ?? string.Empty;

        return buffView != -1;
    }

    private static bool GetBufferViewInfo(JsonElement root, int index, out int buffer, out int offset, out int length, out int stride)
    {
        buffer = -1; offset = 0; length = 0; stride = 0;
        if (!root.TryGetProperty("bufferViews", out var views) || index < 0 || index >= views.GetArrayLength()) return false;

        var view = views[index];
        if (view.TryGetProperty("buffer", out var bufElem)) buffer = bufElem.GetInt32();
        if (view.TryGetProperty("byteOffset", out var offElem)) offset = offElem.GetInt32();
        if (view.TryGetProperty("byteLength", out var lenElem)) length = lenElem.GetInt32();
        if (view.TryGetProperty("byteStride", out var strElem)) stride = strElem.GetInt32();

        return buffer != -1;
    }

    private static int GetSourceIndex(int[]? indices, int position) => indices?[position] ?? position;

    private static byte[]? GetBuffer(byte[]?[] buffers, int index)
        => index >= 0 && index < buffers.Length ? buffers[index] : null;

    private static byte[]?[] LoadBuffers(JsonElement root, string modelPath, byte[]? glbBinChunk, List<string> dependencies)
    {
        if (!root.TryGetProperty("buffers", out var buffersProp) || buffersProp.ValueKind != JsonValueKind.Array || buffersProp.GetArrayLength() == 0)
            return [glbBinChunk];

        int count = buffersProp.GetArrayLength();
        var result = new byte[]?[count];
        result[0] = glbBinChunk;

        long maxTotalBytes = Model3DSettings.Default.MaxFileSizeBytes;
        long totalBytes = glbBinChunk?.LongLength ?? 0;

        for (int i = 0; i < count; i++)
        {
            if (result[i] != null) continue;
            if (!buffersProp[i].TryGetProperty("uri", out var uriProp)) continue;

            var uri = uriProp.GetString();
            if (string.IsNullOrEmpty(uri)) continue;

            if (uri.StartsWith("data:", StringComparison.Ordinal))
            {
                int separator = uri.IndexOf(',');
                if (separator < 0) continue;
                try
                {
                    var decoded = Convert.FromBase64String(uri[(separator + 1)..]);
                    if (totalBytes + decoded.LongLength > maxTotalBytes) continue;
                    totalBytes += decoded.LongLength;
                    result[i] = decoded;
                }
                catch (FormatException)
                {
                }
            }
            else
            {
                string binPath = ResolveExternalUri(uri, modelPath);
                if (binPath.Length == 0) continue;

                dependencies.Add(binPath);

                try
                {
                    if (!File.Exists(binPath)) continue;
                    long length = new FileInfo(binPath).Length;
                    if (!Model3DSettings.Default.IsFileSizeAllowed(length)) continue;
                    if (totalBytes + length > maxTotalBytes) continue;
                    totalBytes += length;
                    result[i] = File.ReadAllBytes(binPath);
                }
                catch
                {
                }
            }
        }
        return result;
    }

    private static string ResolveExternalUri(string uri, string modelPath)
    {
        try
        {
            string decoded = Uri.UnescapeDataString(uri);
            if (Path.IsPathRooted(decoded)) return string.Empty;

            string directory = Path.GetFullPath(Path.GetDirectoryName(modelPath) ?? string.Empty);
            string resolved = Path.GetFullPath(Path.Combine(directory, decoded));
            if (!resolved.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            return resolved;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record GlbContext(
        JsonElement Root,
        byte[]?[] Buffers,
        JsonElement Nodes,
        JsonElement Materials,
        List<string> Images,
        List<(int Source, byte U, byte V)> Textures,
        List<Model3DVertex> Vertices,
        List<int> Indices,
        List<Model3DPart> Parts,
        List<(int Start, int Count)> MissingNormalRanges)
    {
        public bool LimitExceeded { get; set; }
    }
}
