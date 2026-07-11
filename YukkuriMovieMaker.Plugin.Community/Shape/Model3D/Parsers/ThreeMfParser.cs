using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Xml;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class ThreeMfParser : IModelParser
{
    private const int MaxComponentDepth = 32;

    private static readonly string[] FileExtensions = [".3mf"];

    public string Id => "ThreeMf";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    public Model3DData Parse(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var modelEntry = archive.GetEntry("3D/3dmodel.model");
            if (modelEntry == null) return new Model3DData();

            using var stream = modelEntry.Open();
            using var reader = XmlReader.Create(stream);

            var objects = new Dictionary<string, ObjectResource>();
            var orderedObjects = new List<ObjectResource>();
            var buildItems = new List<(string ObjectId, Matrix4x4 Transform)>();
            var colorMap = new Dictionary<string, Vector4>();

            string currentResourcePid = "";
            int resourceIndex = 0;
            ObjectResource? currentObject = null;
            string objectPid = "";
            string objectP1 = "";
            bool inBuild = false;

            var limits = Model3DSettings.Default;
            long totalVertices = 0;
            long totalTriangles = 0;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "basematerials" || reader.LocalName == "colorgroup")
                    {
                        currentResourcePid = reader.GetAttribute("id") ?? "";
                        resourceIndex = 0;
                    }
                    else if (reader.LocalName == "base")
                    {
                        string val = reader.GetAttribute("displaycolor") ?? "#FFFFFFFF";
                        if (ParseColor(val, out var col)) colorMap[currentResourcePid + ":" + resourceIndex] = col;
                        resourceIndex++;
                    }
                    else if (reader.LocalName == "color")
                    {
                        string val = reader.GetAttribute("color") ?? "#FFFFFFFF";
                        if (ParseColor(val, out var col)) colorMap[currentResourcePid + ":" + resourceIndex] = col;
                        resourceIndex++;
                    }
                    else if (reader.LocalName == "object")
                    {
                        currentObject = new ObjectResource();
                        orderedObjects.Add(currentObject);
                        string id = reader.GetAttribute("id") ?? "";
                        if (id.Length > 0) objects[id] = currentObject;
                        objectPid = reader.GetAttribute("pid") ?? "";
                        objectP1 = reader.GetAttribute("p1") ?? "";
                    }
                    else if (reader.LocalName == "component")
                    {
                        if (currentObject != null)
                        {
                            string objectId = reader.GetAttribute("objectid") ?? "";
                            if (objectId.Length > 0)
                            {
                                currentObject.Components.Add((objectId, ParseTransform(reader.GetAttribute("transform"))));
                            }
                        }
                    }
                    else if (reader.LocalName == "vertex")
                    {
                        if (currentObject != null)
                        {
                            if (++totalVertices > limits.MaxVertices) return new Model3DData();
                            float x = float.Parse(reader.GetAttribute("x") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                            float y = float.Parse(reader.GetAttribute("y") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                            float z = float.Parse(reader.GetAttribute("z") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                            currentObject.Vertices.Add(new Vector3(x, y, z));
                        }
                    }
                    else if (reader.LocalName == "triangle")
                    {
                        if (currentObject != null)
                        {
                            if (++totalTriangles * 3 > limits.MaxIndices) return new Model3DData();
                            int v1 = int.Parse(reader.GetAttribute("v1") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
                            int v2 = int.Parse(reader.GetAttribute("v2") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
                            int v3 = int.Parse(reader.GetAttribute("v3") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);

                            string? pid = reader.GetAttribute("pid") ?? objectPid;
                            string? p1 = reader.GetAttribute("p1") ?? (string.IsNullOrEmpty(reader.GetAttribute("pid")) ? objectP1 : "");

                            Vector4 c1 = ResolveColor(colorMap, pid, p1, Vector4.One);
                            Vector4 c2 = ResolveColor(colorMap, pid, reader.GetAttribute("p2"), c1);
                            Vector4 c3 = ResolveColor(colorMap, pid, reader.GetAttribute("p3"), c1);

                            currentObject.Triangles.Add(new Triangle(v1, v2, v3, c1, c2, c3));
                        }
                    }
                    else if (reader.LocalName == "build")
                    {
                        inBuild = true;
                    }
                    else if (reader.LocalName == "item")
                    {
                        if (inBuild)
                        {
                            string objectId = reader.GetAttribute("objectid") ?? "";
                            if (objectId.Length > 0)
                            {
                                buildItems.Add((objectId, ParseTransform(reader.GetAttribute("transform"))));
                            }
                        }
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "object")
                    {
                        currentObject = null;
                        objectPid = "";
                        objectP1 = "";
                    }
                    else if (reader.LocalName == "build")
                    {
                        inBuild = false;
                    }
                }
            }

            var emitter = new SceneEmitter(objects);

            if (buildItems.Count > 0)
            {
                foreach (var (objectId, transform) in buildItems)
                {
                    emitter.EmitObject(objectId, transform);
                }
            }
            else
            {
                foreach (var obj in orderedObjects)
                {
                    emitter.EmitResource(obj, Matrix4x4.Identity, [], 0);
                }
            }

            return emitter.Build();
        }
        catch
        {
            return new Model3DData();
        }
    }

    private static Matrix4x4 ParseTransform(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Matrix4x4.Identity;

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 12) return Matrix4x4.Identity;

        Span<float> m = stackalloc float[12];
        for (int i = 0; i < 12; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out m[i])) return Matrix4x4.Identity;
        }

        return new Matrix4x4(
            m[0], m[1], m[2], 0,
            m[3], m[4], m[5], 0,
            m[6], m[7], m[8], 0,
            m[9], m[10], m[11], 1);
    }

    private static bool ParseColor(string hex, out Vector4 color)
    {
        color = Vector4.One;
        if (string.IsNullOrEmpty(hex)) return false;
        try
        {
            int start = hex.StartsWith("#") ? 1 : 0;
            string cleanHex = hex.Substring(start);
            if (cleanHex.Length == 8)
            {
                byte r = Convert.ToByte(cleanHex.Substring(0, 2), 16);
                byte g = Convert.ToByte(cleanHex.Substring(2, 2), 16);
                byte b = Convert.ToByte(cleanHex.Substring(4, 2), 16);
                byte a = Convert.ToByte(cleanHex.Substring(6, 2), 16);
                color = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
                return true;
            }
            else if (cleanHex.Length == 6)
            {
                byte r = Convert.ToByte(cleanHex.Substring(0, 2), 16);
                byte g = Convert.ToByte(cleanHex.Substring(2, 2), 16);
                byte b = Convert.ToByte(cleanHex.Substring(4, 2), 16);
                color = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static Vector4 ResolveColor(Dictionary<string, Vector4> colorMap, string? pid, string? index, Vector4 fallback)
    {
        if (string.IsNullOrEmpty(pid) || string.IsNullOrEmpty(index)) return fallback;
        return colorMap.TryGetValue(pid + ":" + index, out var color) ? color : fallback;
    }

    private readonly record struct Triangle(int V1, int V2, int V3, Vector4 Color1, Vector4 Color2, Vector4 Color3);

    private sealed class ObjectResource
    {
        public List<Vector3> Vertices { get; } = [];
        public List<Triangle> Triangles { get; } = [];
        public List<(string ObjectId, Matrix4x4 Transform)> Components { get; } = [];
    }

    private sealed class SceneEmitter(Dictionary<string, ObjectResource> objects)
    {
        private readonly List<Model3DVertex> _vertices = [];
        private readonly Dictionary<Vector4, List<int>> _groupedIndices = [];
        private readonly HashSet<Vector4> _transparentGroups = [];
        private long _emittedIndexCount;

        public void EmitObject(string objectId, Matrix4x4 transform)
        {
            if (objects.TryGetValue(objectId, out var obj))
            {
                EmitResource(obj, transform, [], 0);
            }
        }

        public void EmitResource(ObjectResource obj, Matrix4x4 transform, HashSet<ObjectResource> stack, int depth)
        {
            if (depth > MaxComponentDepth || !stack.Add(obj)) return;

            var limits = Model3DSettings.Default;

            if (obj.Triangles.Count > 0)
            {
                var cornerMap = new Dictionary<(int Vertex, Vector4 Color), int>();

                foreach (var tri in obj.Triangles)
                {
                    if (_emittedIndexCount + 3 > limits.MaxIndices) break;
                    if ((uint)tri.V1 >= (uint)obj.Vertices.Count
                        || (uint)tri.V2 >= (uint)obj.Vertices.Count
                        || (uint)tri.V3 >= (uint)obj.Vertices.Count) continue;

                    if (!TryEmitCorner(obj, transform, cornerMap, limits, tri.V1, tri.Color1, out int i1)
                        || !TryEmitCorner(obj, transform, cornerMap, limits, tri.V2, tri.Color2, out int i2)
                        || !TryEmitCorner(obj, transform, cornerMap, limits, tri.V3, tri.Color3, out int i3)) break;

                    if (!_groupedIndices.TryGetValue(tri.Color1, out var group))
                    {
                        group = [];
                        _groupedIndices[tri.Color1] = group;
                    }
                    group.Add(i1);
                    group.Add(i2);
                    group.Add(i3);
                    _emittedIndexCount += 3;

                    if (tri.Color1.W < 1.0f || tri.Color2.W < 1.0f || tri.Color3.W < 1.0f)
                    {
                        _transparentGroups.Add(tri.Color1);
                    }
                }
            }

            foreach (var (childId, childTransform) in obj.Components)
            {
                if (objects.TryGetValue(childId, out var child))
                {
                    EmitResource(child, childTransform * transform, stack, depth + 1);
                }
            }

            stack.Remove(obj);
        }

        private bool TryEmitCorner(ObjectResource obj, Matrix4x4 transform, Dictionary<(int Vertex, Vector4 Color), int> cornerMap, Model3DSettings limits, int vertexIndex, Vector4 color, out int emittedIndex)
        {
            if (cornerMap.TryGetValue((vertexIndex, color), out emittedIndex)) return true;

            if (_vertices.Count >= limits.MaxVertices)
            {
                emittedIndex = -1;
                return false;
            }

            var p = Vector3.Transform(obj.Vertices[vertexIndex], transform);
            emittedIndex = _vertices.Count;
            _vertices.Add(new Model3DVertex { Position = new Vector3(p.X, p.Z, -p.Y), Color = color });
            cornerMap[(vertexIndex, color)] = emittedIndex;
            return true;
        }

        public Model3DData Build()
        {
            var vArray = _vertices.ToArray();
            var allIndices = new List<int>();
            var parts = new List<Model3DPart>();
            ModelHelper.CalculateBounds(vArray, out Vector3 c, out float s);

            foreach (var group in _groupedIndices)
            {
                parts.Add(new Model3DPart
                {
                    IndexOffset = allIndices.Count,
                    IndexCount = group.Value.Count,
                    BaseColor = Vector4.One,
                    ForceTransparent = _transparentGroups.Contains(group.Key) || group.Key.W < 1.0f
                });
                allIndices.AddRange(group.Value);
            }

            var iArray = allIndices.ToArray();
            ModelHelper.CalculateNormals(vArray, iArray);

            return new Model3DData { Vertices = vArray, Indices = iArray, Parts = parts, ModelCenter = c, ModelScale = s };
        }
    }
}
