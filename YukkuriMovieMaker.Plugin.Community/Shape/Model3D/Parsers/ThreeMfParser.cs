using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Xml;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class ThreeMfParser : IModelParser
{
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

            var verts = new List<Model3DVertex>();
            var colorMap = new Dictionary<string, Vector4>();
            var groupedIndices = new Dictionary<Vector4, List<int>>();

            string currentResourcePid = "";
            int resourceIndex = 0;
            string objectPid = "";
            string objectP1 = "";
            int vertexOffset = 0;

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
                        objectPid = reader.GetAttribute("pid") ?? "";
                        objectP1 = reader.GetAttribute("p1") ?? "";
                    }
                    else if (reader.LocalName == "mesh")
                    {
                        vertexOffset = verts.Count;
                    }
                    else if (reader.LocalName == "vertex")
                    {
                        float x = float.Parse(reader.GetAttribute("x") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                        float y = float.Parse(reader.GetAttribute("y") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                        float z = float.Parse(reader.GetAttribute("z") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
                        verts.Add(new Model3DVertex { Position = new Vector3(x, z, -y), Color = Vector4.One });
                    }
                    else if (reader.LocalName == "triangle")
                    {
                        int v1 = int.Parse(reader.GetAttribute("v1") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture) + vertexOffset;
                        int v2 = int.Parse(reader.GetAttribute("v2") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture) + vertexOffset;
                        int v3 = int.Parse(reader.GetAttribute("v3") ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture) + vertexOffset;

                        string? pid = reader.GetAttribute("pid") ?? objectPid;
                        string? p1 = reader.GetAttribute("p1") ?? (string.IsNullOrEmpty(reader.GetAttribute("pid")) ? objectP1 : "");

                        Vector4 triColor = Vector4.One;
                        if (!string.IsNullOrEmpty(pid) && !string.IsNullOrEmpty(p1))
                        {
                            if (colorMap.TryGetValue(pid + ":" + p1, out var col)) triColor = col;
                        }

                        if (!groupedIndices.ContainsKey(triColor)) groupedIndices[triColor] = new List<int>();
                        groupedIndices[triColor].Add(v1);
                        groupedIndices[triColor].Add(v2);
                        groupedIndices[triColor].Add(v3);

                        if (v1 < verts.Count) { var v = verts[v1]; v.Color = triColor; verts[v1] = v; }
                        if (v2 < verts.Count) { var v = verts[v2]; v.Color = triColor; verts[v2] = v; }
                        if (v3 < verts.Count) { var v = verts[v3]; v.Color = triColor; verts[v3] = v; }
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "object") { objectPid = ""; objectP1 = ""; }
                }
            }

            var vArray = verts.ToArray();
            var allIndices = new List<int>();
            var parts = new List<Model3DPart>();
            ModelHelper.CalculateBounds(vArray, out Vector3 c, out float s);

            foreach (var group in groupedIndices)
            {
                parts.Add(new Model3DPart
                {
                    IndexOffset = allIndices.Count,
                    IndexCount = group.Value.Count,
                    BaseColor = group.Key
                });
                allIndices.AddRange(group.Value);
            }

            var iArray = allIndices.ToArray();
            ModelHelper.CalculateNormals(vArray, iArray);

            return new Model3DData { Vertices = vArray, Indices = iArray, Parts = parts, ModelCenter = c, ModelScale = s };
        }
        catch
        {
            return new Model3DData();
        }
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
}
