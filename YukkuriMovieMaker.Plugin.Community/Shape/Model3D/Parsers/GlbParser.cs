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
        catch
        {
            return new Model3DData();
        }

        if (string.IsNullOrEmpty(jsonStr)) return new Model3DData();

        var allVertices = new List<Model3DVertex>();
        var allIndices = new List<int>();
        var parts = new List<Model3DPart>();

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            binData ??= TryLoadExternalBuffer(root, path);

            if (root.TryGetProperty("extensionsRequired", out var exts))
            {
                foreach (var ext in exts.EnumerateArray())
                {
                    if (ext.GetString() == "KHR_draco_mesh_compression")
                    {
                        return new Model3DData();
                    }
                }
            }

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
                        if (binData != null && GetBufferViewInfo(root, bvProp.GetInt32(), out int bIdx, out int bOff, out int bLen, out _))
                        {
                            if (bIdx == 0 && bOff >= 0 && bLen > 0 && (long)bOff + bLen <= binData.Length)
                            {
                                imgBytes = new byte[bLen];
                                Array.Copy(binData, bOff, imgBytes, 0, bLen);
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
                                if (separator >= 0)
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
                            }
                        }
                    }

                    images.Add(imgBytes != null
                        ? ModelHelper.WriteEmbeddedTexture(path, images.Count, ext, imgBytes)
                        : externalPath);
                }
            }

            var textures = new List<int>();
            if (root.TryGetProperty("textures", out var texProp))
            {
                foreach (var tex in texProp.EnumerateArray())
                {
                    if (tex.TryGetProperty("source", out var srcProp))
                    {
                        textures.Add(srcProp.GetInt32());
                    }
                    else
                    {
                        textures.Add(-1);
                    }
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
            if (root.TryGetProperty("scene", out var defaultSceneIdx))
            {
                if (root.TryGetProperty("scenes", out var scenes) && scenes.ValueKind == JsonValueKind.Array)
                {
                    var idx = defaultSceneIdx.GetInt32();
                    if (idx >= 0 && idx < scenes.GetArrayLength())
                    {
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
            }

            var context = new GlbContext(root, binData, nodes, materials, images, textures, allVertices, allIndices, parts);

            if (sceneNodes.Count == 0 && meshes.ValueKind == JsonValueKind.Array)
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
        }
        catch
        {
            return new Model3DData();
        }

        var verticesArr = allVertices.ToArray();
        var indicesArr = allIndices.ToArray();

        bool calcNormals = true;
        for (int i = 0; i < verticesArr.Length; i++)
        {
            if (verticesArr[i].Normal != Vector3.Zero)
            {
                calcNormals = false;
                break;
            }
        }

        if (calcNormals)
        {
            ModelHelper.CalculateNormals(verticesArr, indicesArr);
        }

        ModelHelper.CalculateBounds(verticesArr, out Vector3 c, out float s);

        return new Model3DData
        {
            Vertices = verticesArr,
            Indices = indicesArr,
            Parts = parts,
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
        var (root, binData, _, materials, images, textures, allVertices, allIndices, parts) = context;
        if (!root.TryGetProperty("meshes", out var meshes) || meshIdx < 0 || meshIdx >= meshes.GetArrayLength()) return;

        var mesh = meshes[meshIdx];

        if (mesh.TryGetProperty("primitives", out var primitives))
        {
            var limits = Model3DSettings.Default;

            foreach (var prim in primitives.EnumerateArray())
            {
                if (allVertices.Count > limits.MaxVertices || allIndices.Count > limits.MaxIndices || parts.Count > limits.MaxParts) return;
                if (!prim.TryGetProperty("attributes", out var attrs)) continue;
                if (!attrs.TryGetProperty("POSITION", out var posAccIdxElem)) continue;

                int posAccIdx = posAccIdxElem.GetInt32();
                int normAccIdx = attrs.TryGetProperty("NORMAL", out var normElem) ? normElem.GetInt32() : -1;
                int uvAccIdx = attrs.TryGetProperty("TEXCOORD_0", out var uvElem) ? uvElem.GetInt32() : -1;
                int colAccIdx = attrs.TryGetProperty("COLOR_0", out var colElem) ? colElem.GetInt32() : -1;
                int indAccIdx = prim.TryGetProperty("indices", out var indElem) ? indElem.GetInt32() : -1;
                int matIdx = prim.TryGetProperty("material", out var matElem) ? matElem.GetInt32() : -1;

                var positions = ReadVector3Array(root, binData, posAccIdx);
                if (positions == null || positions.Length == 0) continue;

                var normals = normAccIdx >= 0 ? ReadVector3Array(root, binData, normAccIdx) : null;
                var uvs = uvAccIdx >= 0 ? ReadVector2Array(root, binData, uvAccIdx) : null;
                var colors = colAccIdx >= 0 ? ReadVector4Array(root, binData, colAccIdx) : null;
                var indices = indAccIdx >= 0 ? ReadIntArray(root, binData, indAccIdx) : null;

                int indexAddCount = indices?.Length ?? positions.Length;
                if (positions.Length > limits.MaxVertices - allVertices.Count || indexAddCount > limits.MaxIndices - allIndices.Count) return;

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
                        Color = (colors != null && i < colors.Length) ? colors[i] : Vector4.One
                    };
                    allVertices.Add(v);
                }

                int startIndex = allIndices.Count;
                if (indices != null)
                {
                    foreach (var idx in indices)
                    {
                        allIndices.Add(idx + vertexOffset);
                    }
                }
                else
                {
                    for (int i = 0; i < positions.Length; i++)
                    {
                        allIndices.Add(i + vertexOffset);
                    }
                }

                Vector4 baseColor = Vector4.One;
                float metallic = Model3DPart.DefaultMetallic;
                float roughness = Model3DPart.DefaultRoughness;
                string texPath = string.Empty;
                string metallicRoughnessTexPath = string.Empty;

                if (matIdx >= 0 && materials.ValueKind == JsonValueKind.Array && matIdx < materials.GetArrayLength())
                {
                    var mat = materials[matIdx];
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
                                    int imgIdx = textures[bTexIdx];
                                    if (imgIdx >= 0 && imgIdx < images.Count)
                                    {
                                        texPath = images[imgIdx];
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
                                int imgIdx = textures[mrTexIdx];
                                if (imgIdx >= 0 && imgIdx < images.Count)
                                {
                                    metallicRoughnessTexPath = images[imgIdx];
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
                    Roughness = roughness
                });
            }
        }
    }

    private static Vector3[]? ReadVector3Array(JsonElement root, byte[]? binData, int accessorIdx)
    {
        if (binData == null) return null;
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out _)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        if (stride <= 0) stride = 12;
        if (!TryClampAccessorCount(binData, viewOffset, offset, stride, 12, ref count, out int start)) return null;

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

    private static Vector2[]? ReadVector2Array(JsonElement root, byte[]? binData, int accessorIdx)
    {
        if (binData == null) return null;
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        int componentSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        int elementSize = componentSize * 2;
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, offset, stride, elementSize, ref count, out int start)) return null;

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
            result[i] = new Vector2(x, 1.0f - y);
        }
        return result;
    }

    private static Vector4[]? ReadVector4Array(JsonElement root, byte[]? binData, int accessorIdx)
    {
        if (binData == null) return null;
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType, out string type)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        int componentCount = type == "VEC3" ? 3 : 4;
        int componentSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        int elementSize = componentSize * componentCount;
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, offset, stride, elementSize, ref count, out int start)) return null;

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

    private static int[]? ReadIntArray(JsonElement root, byte[]? binData, int accessorIdx)
    {
        if (binData == null) return null;
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        int elementSize = compType == ComponentTypeUByte ? 1 : (compType == ComponentTypeUShort ? 2 : 4);
        if (stride <= 0) stride = elementSize;
        if (!TryClampAccessorCount(binData, viewOffset, offset, stride, elementSize, ref count, out int start)) return null;

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

    private static bool TryClampAccessorCount(byte[] binData, int viewOffset, int offset, int stride, int elementSize, ref int count, out int start)
    {
        start = 0;
        if (viewOffset < 0 || offset < 0 || count < 0) return false;

        long longStart = (long)viewOffset + offset;
        long available = binData.Length - longStart;
        if (longStart > int.MaxValue || available < elementSize) return false;

        start = (int)longStart;
        long maxCount = (available - elementSize) / stride + 1;
        if (count > maxCount) count = (int)maxCount;
        return true;
    }

    private static bool GetAccessorInfo(JsonElement root, int index, out int buffView, out int offset, out int count, out int compType)
        => GetAccessorInfo(root, index, out buffView, out offset, out count, out compType, out _);

    private static bool GetAccessorInfo(JsonElement root, int index, out int buffView, out int offset, out int count, out int compType, out string type)
    {
        buffView = -1; offset = 0; count = 0; compType = 0; type = string.Empty;
        if (!root.TryGetProperty("accessors", out var accessors) || index < 0 || index >= accessors.GetArrayLength()) return false;

        var acc = accessors[index];
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

    private static byte[]? TryLoadExternalBuffer(JsonElement root, string modelPath)
    {
        if (!root.TryGetProperty("buffers", out var buffers) || buffers.ValueKind != JsonValueKind.Array || buffers.GetArrayLength() == 0) return null;
        if (!buffers[0].TryGetProperty("uri", out var uriProp)) return null;

        var uri = uriProp.GetString();
        if (string.IsNullOrEmpty(uri)) return null;

        if (uri.StartsWith("data:", StringComparison.Ordinal))
        {
            int separator = uri.IndexOf(',');
            if (separator < 0) return null;
            try
            {
                return Convert.FromBase64String(uri[(separator + 1)..]);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        string binPath = ResolveExternalUri(uri, modelPath);
        if (binPath.Length == 0) return null;
        if (!Model3DSettings.Default.IsFileSizeAllowed(new FileInfo(binPath).Length)) return null;
        return File.ReadAllBytes(binPath);
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

            return File.Exists(resolved) ? resolved : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record GlbContext(
        JsonElement Root,
        byte[]? BinData,
        JsonElement Nodes,
        JsonElement Materials,
        List<string> Images,
        List<int> Textures,
        List<Model3DVertex> Vertices,
        List<int> Indices,
        List<Model3DPart> Parts);
}
