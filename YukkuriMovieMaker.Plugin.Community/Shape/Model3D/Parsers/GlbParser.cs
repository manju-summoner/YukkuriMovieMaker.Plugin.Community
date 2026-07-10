using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class GlbParser : IModelParser
{
    private static readonly string[] FileExtensions = [".glb", ".gltf"];

    public string Id => "Glb";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        if (!File.Exists(path)) return new Model3DData();

        byte[]? binData = null;
        string jsonStr = "";

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadUInt32();
            if (magic != 0x46546C67)
            {
                return new Model3DData();
            }

            br.ReadUInt32();
            var length = br.ReadUInt32();

            if (fs.Position + 8 > length) return new Model3DData();
            var chunkLength = br.ReadInt32();
            var chunkType = br.ReadUInt32();

            if (chunkType != 0x4E4F534A) return new Model3DData();
            var jsonBytes = br.ReadBytes(chunkLength);
            jsonStr = Encoding.UTF8.GetString(jsonBytes);

            if (fs.Position < length)
            {
                var binLength = br.ReadInt32();
                var binType = br.ReadUInt32();
                if (binType == 0x004E4942)
                {
                    binData = br.ReadBytes(binLength);
                }
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
                    if (img.TryGetProperty("bufferView", out var bvProp))
                    {
                        if (binData != null && GetBufferViewInfo(root, bvProp.GetInt32(), out int bIdx, out int bOff, out int bLen, out _))
                        {
                            if (bIdx == 0 && bOff + bLen <= binData.Length)
                            {
                                imgBytes = new byte[bLen];
                                Array.Copy(binData, bOff, imgBytes, 0, bLen);
                            }
                        }
                    }
                    else if (img.TryGetProperty("uri", out var uriProp))
                    {
                        var uri = uriProp.GetString();
                        if (!string.IsNullOrEmpty(uri) && uri.StartsWith("data:image"))
                        {
                            var base64 = uri.Substring(uri.IndexOf(",") + 1);
                            imgBytes = Convert.FromBase64String(base64);
                        }
                    }

                    images.Add(imgBytes != null
                        ? ModelHelper.WriteEmbeddedTexture(path, images.Count, ext, imgBytes)
                        : string.Empty);
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

            if (sceneNodes.Count == 0 && meshes.ValueKind == JsonValueKind.Array)
            {
                int meshCount = meshes.GetArrayLength();
                for (int i = 0; i < meshCount; i++)
                {
                    ProcessMesh(root, binData, i, Matrix4x4.Identity, allVertices, allIndices, parts, materials, images, textures);
                }
            }
            else
            {
                foreach (var nodeIdx in sceneNodes)
                {
                    TraverseNode(root, binData, nodeIdx, Matrix4x4.Identity, allVertices, allIndices, parts, nodes, materials, images, textures);
                }
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

    private static void TraverseNode(JsonElement root, byte[]? binData, int nodeIdx, Matrix4x4 parentTransform, List<Model3DVertex> vertices, List<int> indices, List<Model3DPart> parts, JsonElement nodes, JsonElement materials, List<string> images, List<int> textures)
    {
        if (nodes.ValueKind != JsonValueKind.Array || nodeIdx < 0 || nodeIdx >= nodes.GetArrayLength()) return;

        var node = nodes[nodeIdx];
        Matrix4x4 localTransform;

        if (node.TryGetProperty("matrix", out var matProp) && matProp.GetArrayLength() == 16)
        {
            var m = new float[16];
            int i = 0;
            foreach (var val in matProp.EnumerateArray()) m[i++] = val.GetSingle();
            localTransform = new Matrix4x4(
                m[0], m[1], m[2], m[3],
                m[4], m[5], m[6], m[7],
                m[8], m[9], m[10], m[11],
                m[12], m[13], m[14], m[15]
            );
        }
        else
        {
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

            localTransform = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
        }

        var worldTransform = localTransform * parentTransform;

        if (node.TryGetProperty("mesh", out var meshIdxProp))
        {
            ProcessMesh(root, binData, meshIdxProp.GetInt32(), worldTransform, vertices, indices, parts, materials, images, textures);
        }

        if (node.TryGetProperty("children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var childIdx in childrenProp.EnumerateArray())
            {
                TraverseNode(root, binData, childIdx.GetInt32(), worldTransform, vertices, indices, parts, nodes, materials, images, textures);
            }
        }
    }

    private static void ProcessMesh(JsonElement root, byte[]? binData, int meshIdx, Matrix4x4 transform, List<Model3DVertex> allVertices, List<int> allIndices, List<Model3DPart> parts, JsonElement materials, List<string> images, List<int> textures)
    {
        if (!root.TryGetProperty("meshes", out var meshes) || meshIdx < 0 || meshIdx >= meshes.GetArrayLength()) return;

        var mesh = meshes[meshIdx];

        if (mesh.TryGetProperty("primitives", out var primitives))
        {
            foreach (var prim in primitives.EnumerateArray())
            {
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

                int vertexOffset = allVertices.Count;

                for (int i = 0; i < positions.Length; i++)
                {
                    var p = Vector3.Transform(positions[i], transform);
                    var n = Vector3.Zero;
                    if (normals != null && i < normals.Length)
                    {
                        n = Vector3.TransformNormal(normals[i], transform);
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
                    }
                }

                parts.Add(new Model3DPart
                {
                    TexturePath = texPath,
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

        if (stride == 0) stride = 12;

        var result = new Vector3[count];
        int start = viewOffset + offset;

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
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out _)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        if (stride == 0) stride = 8;

        var result = new Vector2[count];
        int start = viewOffset + offset;

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + 8 > binData.Length) break;

            float x = BitConverter.ToSingle(binData, p);
            float y = BitConverter.ToSingle(binData, p + 4);
            result[i] = new Vector2(x, 1.0f - y);
        }
        return result;
    }

    private static Vector4[]? ReadVector4Array(JsonElement root, byte[]? binData, int accessorIdx)
    {
        if (binData == null) return null;
        if (!GetAccessorInfo(root, accessorIdx, out int buffViewIdx, out int offset, out int count, out int compType)) return null;
        if (!GetBufferViewInfo(root, buffViewIdx, out int buffIdx, out int viewOffset, out _, out int stride)) return null;
        if (buffIdx != 0) return null;

        int elementSize = compType == 5121 ? 4 : (compType == 5123 ? 8 : 16);
        if (stride == 0) stride = elementSize;

        var result = new Vector4[count];
        int start = viewOffset + offset;

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + elementSize > binData.Length) break;

            float x = 0, y = 0, z = 0, w = 1;

            if (compType == 5126)
            {
                x = BitConverter.ToSingle(binData, p);
                y = BitConverter.ToSingle(binData, p + 4);
                z = BitConverter.ToSingle(binData, p + 8);
                w = BitConverter.ToSingle(binData, p + 12);
            }
            else if (compType == 5121)
            {
                x = binData[p] / 255.0f;
                y = binData[p + 1] / 255.0f;
                z = binData[p + 2] / 255.0f;
                w = binData[p + 3] / 255.0f;
            }
            else if (compType == 5123)
            {
                x = BitConverter.ToUInt16(binData, p) / 65535.0f;
                y = BitConverter.ToUInt16(binData, p + 2) / 65535.0f;
                z = BitConverter.ToUInt16(binData, p + 4) / 65535.0f;
                w = BitConverter.ToUInt16(binData, p + 6) / 65535.0f;
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

        var result = new int[count];
        int start = viewOffset + offset;

        int elementSize = compType == 5121 ? 1 : (compType == 5123 ? 2 : 4);
        if (stride == 0) stride = elementSize;

        for (int i = 0; i < count; i++)
        {
            int p = start + i * stride;
            if (p + elementSize > binData.Length) break;

            if (compType == 5121)
            {
                result[i] = binData[p];
            }
            else if (compType == 5123)
            {
                result[i] = BitConverter.ToUInt16(binData, p);
            }
            else if (compType == 5125)
            {
                result[i] = (int)BitConverter.ToUInt32(binData, p);
            }
        }
        return result;
    }

    private static bool GetAccessorInfo(JsonElement root, int index, out int buffView, out int offset, out int count, out int compType)
    {
        buffView = -1; offset = 0; count = 0; compType = 0;
        if (!root.TryGetProperty("accessors", out var accessors) || index >= accessors.GetArrayLength()) return false;

        var acc = accessors[index];
        if (acc.TryGetProperty("bufferView", out var bvElem)) buffView = bvElem.GetInt32();
        if (acc.TryGetProperty("byteOffset", out var offElem)) offset = offElem.GetInt32();
        if (acc.TryGetProperty("count", out var cntElem)) count = cntElem.GetInt32();
        if (acc.TryGetProperty("componentType", out var typeElem)) compType = typeElem.GetInt32();

        return buffView != -1;
    }

    private static bool GetBufferViewInfo(JsonElement root, int index, out int buffer, out int offset, out int length, out int stride)
    {
        buffer = -1; offset = 0; length = 0; stride = 0;
        if (!root.TryGetProperty("bufferViews", out var views) || index >= views.GetArrayLength()) return false;

        var view = views[index];
        if (view.TryGetProperty("buffer", out var bufElem)) buffer = bufElem.GetInt32();
        if (view.TryGetProperty("byteOffset", out var offElem)) offset = offElem.GetInt32();
        if (view.TryGetProperty("byteLength", out var lenElem)) length = lenElem.GetInt32();
        if (view.TryGetProperty("byteStride", out var strElem)) stride = strElem.GetInt32();

        return buffer != -1;
    }
}
