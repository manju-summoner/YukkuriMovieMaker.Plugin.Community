using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal sealed class WavefrontObjParser : IModelParser
{
    private static readonly string[] FileExtensions = [".obj"];

    public string Id => "Obj";
    public int Version => 1;
    public IReadOnlyList<string> Extensions => FileExtensions;

    private struct MaterialData
    {
        public string TexturePath;
        public Vector4 DiffuseColor;
    }

    private struct SplitEvent
    {
        public int LocalFaceIndex;
        public byte Type;
        public string Name;
    }

    private sealed class ChunkResult
    {
        public List<SplitEvent> Events = [];
        public List<string> MtlLibs = [];
    }

    public unsafe Model3DData Parse(string path)
    {
        using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        byte* basePointer = null;
        Vector3* rawV = null;
        Vector2* rawVt = null;
        Vector3* rawVn = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePointer);

        int processorCount = Environment.ProcessorCount;
        var offsets = new Counts[processorCount];
        var chunkResults = new ChunkResult[processorCount];
        long totalV = 0, totalVt = 0, totalVn = 0, totalF = 0;
        Model3DVertex[] vertices;
        int[] indices;

        try
        {
            long fileSize = accessor.Capacity;
            var chunkBoundaries = new long[processorCount + 1];
            long chunkSize = fileSize / processorCount;
            chunkBoundaries[0] = 0;
            chunkBoundaries[processorCount] = fileSize;

            for (int i = 1; i < processorCount; i++)
            {
                long pos = i * chunkSize;
                while (pos < fileSize && *(basePointer + pos) != '\n') pos++;
                if (pos < fileSize) pos++;
                chunkBoundaries[i] = pos;
            }

            var counts = new Counts[processorCount];

            Parallel.For(0, processorCount, i =>
            {
                counts[i] = CountChunk(basePointer, chunkBoundaries[i], chunkBoundaries[i + 1]);
            });

            for (int i = 0; i < processorCount; i++)
            {
                offsets[i].V = totalV;
                offsets[i].Vt = totalVt;
                offsets[i].Vn = totalVn;
                offsets[i].F = totalF;

                totalV += counts[i].V;
                totalVt += counts[i].Vt;
                totalVn += counts[i].Vn;
                totalF += counts[i].F;
            }

            var limits = Model3DSettings.Default;
            if (totalV > limits.MaxVertices || totalVt > limits.MaxVertices || totalVn > limits.MaxVertices
                || totalF * 3 > limits.MaxIndices)
            {
                return new Model3DData();
            }

            rawV = (Vector3*)NativeMemory.Alloc((nuint)(totalV > 0 ? totalV : 1), (nuint)sizeof(Vector3));
            rawVt = (Vector2*)NativeMemory.Alloc((nuint)(totalVt > 0 ? totalVt : 1), (nuint)sizeof(Vector2));
            rawVn = (Vector3*)NativeMemory.Alloc((nuint)(totalVn > 0 ? totalVn : 1), (nuint)sizeof(Vector3));

            var sortArray = GC.AllocateUninitializedArray<SortableVertex>((int)(totalF * 3), true);

            Parallel.For(0, processorCount, i =>
            {
                chunkResults[i] = ParseChunk(basePointer, chunkBoundaries[i], chunkBoundaries[i + 1],
                    rawV + offsets[i].V,
                    rawVt + offsets[i].Vt,
                    rawVn + offsets[i].Vn,
                    sortArray,
                    (int)(offsets[i].F * 3),
                    (int)(counts[i].F * 3),
                    (int)offsets[i].V,
                    (int)offsets[i].Vt,
                    (int)offsets[i].Vn);
            });

            Array.Sort(sortArray);

            int uniqueCount = 0;
            if (sortArray.Length > 0)
            {
                uniqueCount = 1;
                for (int i = 1; i < sortArray.Length; i++)
                {
                    if (sortArray[i].CompareTo(sortArray[i - 1]) != 0)
                    {
                        uniqueCount++;
                    }
                }
            }

            if (uniqueCount > limits.MaxVertices)
            {
                return new Model3DData();
            }

            vertices = GC.AllocateUninitializedArray<Model3DVertex>(uniqueCount, true);
            indices = GC.AllocateUninitializedArray<int>(sortArray.Length, true);

            if (uniqueCount > 0)
            {
                int currentIdx = 0;

                var first = sortArray[0];
                GetVertexData(first.V, first.Vt, first.Vn, (int)totalV, (int)totalVt, (int)totalVn, rawV, rawVt, rawVn, out Vector3 p, out Vector2 uv, out Vector3 n);
                vertices[0] = new Model3DVertex { Position = p, TexCoord = uv, Normal = n, Color = Vector4.One };
                indices[first.OriginalIndex] = 0;

                for (int i = 1; i < sortArray.Length; i++)
                {
                    var curr = sortArray[i];
                    var prev = sortArray[i - 1];

                    if (curr.CompareTo(prev) != 0)
                    {
                        currentIdx++;
                        GetVertexData(curr.V, curr.Vt, curr.Vn, (int)totalV, (int)totalVt, (int)totalVn, rawV, rawVt, rawVn, out p, out uv, out n);
                        vertices[currentIdx] = new Model3DVertex { Position = p, TexCoord = uv, Normal = n, Color = Vector4.One };
                    }
                    indices[curr.OriginalIndex] = currentIdx;
                }
            }
        }
        finally
        {
            NativeMemory.Free(rawVn);
            NativeMemory.Free(rawVt);
            NativeMemory.Free(rawV);
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        if (indices.Length > 0)
        {
            if (totalVn == 0) ModelHelper.CalculateNormals(vertices, indices);
            else ModelHelper.CalculateMissingNormals(vertices, indices);
        }

        var materialLib = new Dictionary<string, MaterialData>(StringComparer.OrdinalIgnoreCase);
        string baseDir = Path.GetDirectoryName(path) ?? string.Empty;

        var dependencies = new List<string>();
        var seenLibs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var res in chunkResults)
        {
            foreach (var mtlLib in res.MtlLibs)
            {
                if (!seenLibs.Add(mtlLib)) continue;
                ParseMtl(baseDir, mtlLib, materialLib);

                string mtlPath = ModelHelper.ResolveTexturePath(mtlLib, baseDir);
                if (mtlPath.Length > 0) dependencies.Add(mtlPath);
            }
        }

        var parts = new List<Model3DPart>();

        string currentObj = "default";
        string currentGrp = "default";
        string currentMat = "default";

        var allEvents = new List<(int globalFaceIndex, int sequence, SplitEvent evt)>();
        for (int i = 0; i < processorCount; i++)
        {
            int baseF = (int)offsets[i].F;
            foreach (var e in chunkResults[i].Events)
            {
                allEvents.Add((baseF + e.LocalFaceIndex, allEvents.Count, e));
            }
        }
        allEvents.Sort((a, b) =>
        {
            int c = a.globalFaceIndex.CompareTo(b.globalFaceIndex);
            return c != 0 ? c : a.sequence.CompareTo(b.sequence);
        });

        int eventPtr = 0;

        string lastKey = string.Empty;
        string lastMaterial = currentMat;
        int startIndex = 0;

        for (int f = 0; f < totalF; f++)
        {
            while (eventPtr < allEvents.Count && allEvents[eventPtr].globalFaceIndex == f)
            {
                var e = allEvents[eventPtr].evt;
                if (e.Type == 0) currentObj = e.Name;
                else if (e.Type == 1) currentGrp = e.Name;
                else if (e.Type == 2) currentMat = e.Name;
                eventPtr++;
            }

            string key = $"{currentObj}|{currentGrp}|{currentMat}";
            if (f == 0)
            {
                lastKey = key;
                lastMaterial = currentMat;
            }

            if (key != lastKey)
            {
                if (f > startIndex)
                {
                    AddPart(parts, materialLib, lastMaterial, startIndex, f);
                }

                startIndex = f;
                lastKey = key;
                lastMaterial = currentMat;
            }
        }

        if (totalF > startIndex)
        {
            AddPart(parts, materialLib, lastMaterial, startIndex, (int)totalF);
        }

        ModelHelper.CalculateBounds(vertices, out Vector3 center, out float scale);

        return new Model3DData
        {
            Vertices = vertices,
            Indices = indices,
            Parts = parts,
            Dependencies = dependencies,
            ModelCenter = center,
            ModelScale = scale
        };
    }

    private static void AddPart(List<Model3DPart> parts, Dictionary<string, MaterialData> materialLib, string materialName, int startIndex, int endFace)
    {
        var m = materialLib.TryGetValue(materialName, out var md) ? md : new MaterialData { DiffuseColor = Vector4.One };

        parts.Add(new Model3DPart
        {
            IndexOffset = startIndex * 3,
            IndexCount = (endFace - startIndex) * 3,
            TexturePath = m.TexturePath ?? string.Empty,
            BaseColor = m.DiffuseColor
        });
    }

    private static void ParseMtl(string baseDir, string mtlLib, Dictionary<string, MaterialData> lib)
    {
        try
        {
            string path = ModelHelper.ResolveTexturePath(mtlLib, baseDir);
            if (path.Length == 0 || !File.Exists(path)) return;
            if (!Model3DSettings.Default.IsFileSizeAllowed(new FileInfo(path).Length)) return;

            string mtlDir = Path.GetDirectoryName(path) ?? baseDir;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs);
            string? line;
            string currentMat = string.Empty;

            while ((line = sr.ReadLine()) != null)
            {
                var trim = line.Trim();
                if (string.IsNullOrEmpty(trim) || trim.StartsWith("#")) continue;

                var parts = trim.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var keyword = parts[0].ToLowerInvariant();

                if (keyword == "newmtl")
                {
                    currentMat = parts[1];
                    if (!lib.ContainsKey(currentMat))
                    {
                        lib[currentMat] = new MaterialData { DiffuseColor = Vector4.One };
                    }
                }
                else if (!string.IsNullOrEmpty(currentMat))
                {
                    var data = lib[currentMat];
                    if (keyword == "map_kd")
                    {
                        data.TexturePath = ModelHelper.ResolveTexturePath(parts[^1], mtlDir, baseDir);
                        lib[currentMat] = data;
                    }
                    else if (keyword == "kd")
                    {
                        if (parts.Length >= 4 &&
                            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                        {
                            data.DiffuseColor = new Vector4(r, g, b, data.DiffuseColor.W);
                            lib[currentMat] = data;
                        }
                    }
                    else if (keyword == "d")
                    {
                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha))
                        {
                            data.DiffuseColor = new Vector4(data.DiffuseColor.X, data.DiffuseColor.Y, data.DiffuseColor.Z, Math.Clamp(alpha, 0.0f, 1.0f));
                            lib[currentMat] = data;
                        }
                    }
                    else if (keyword == "tr")
                    {
                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float transparency))
                        {
                            data.DiffuseColor = new Vector4(data.DiffuseColor.X, data.DiffuseColor.Y, data.DiffuseColor.Z, Math.Clamp(1.0f - transparency, 0.0f, 1.0f));
                            lib[currentMat] = data;
                        }
                    }
                }
            }
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ResolveIndex(int idx, int definedCount) => idx < 0 ? definedCount + idx + 1 : idx;

    private static unsafe void GetVertexData(int vIdx, int vtIdx, int vnIdx, int vCount, int vtCount, int vnCount,
        Vector3* v, Vector2* vt, Vector3* vn, out Vector3 p, out Vector2 uv, out Vector3 n)
    {
        p = Vector3.Zero;
        if (vIdx > 0 && vIdx <= vCount) p = v[vIdx - 1];

        uv = Vector2.Zero;
        if (vtIdx > 0 && vtIdx <= vtCount) uv = vt[vtIdx - 1];

        n = Vector3.Zero;
        if (vnIdx > 0 && vnIdx <= vnCount) n = vn[vnIdx - 1];
    }

    private static unsafe Counts CountChunk(byte* start, long startOffset, long endOffset)
    {
        var counts = new Counts();
        byte* ptr = start + startOffset;
        byte* end = start + endOffset;

        while (ptr < end)
        {
            while (ptr < end && *ptr <= 32) ptr++;
            if (ptr >= end) break;

            if (*ptr == '#')
            {
                while (ptr < end && *ptr != '\n') ptr++;
                continue;
            }

            byte c1 = *ptr;
            ptr++;
            if (ptr >= end) break;

            if (c1 == 'v')
            {
                byte c2 = *ptr;
                if (c2 == ' ' || c2 == '\t') counts.V++;
                else if (c2 == 't') counts.Vt++;
                else if (c2 == 'n') counts.Vn++;
                while (ptr < end && *ptr != '\n') ptr++;
            }
            else if (c1 == 'f')
            {
                if (*ptr <= 32)
                {
                    long vInFace = 0;
                    while (ptr < end && *ptr != '\n')
                    {
                        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                        if (ptr >= end || *ptr == '\n') break;
                        vInFace++;
                        while (ptr < end && *ptr > 32) ptr++;
                    }
                    if (vInFace >= 3)
                    {
                        counts.F += vInFace - 2;
                    }
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            else
            {
                while (ptr < end && *ptr != '\n') ptr++;
            }
        }
        return counts;
    }

    private static unsafe ChunkResult ParseChunk(byte* start, long startOffset, long endOffset,
        Vector3* vPtr, Vector2* vtPtr, Vector3* vnPtr,
        SortableVertex[] sortArray, int sortStartIndex, int sortCount,
        int vBase, int vtBase, int vnBase)
    {
        var result = new ChunkResult();
        byte* ptr = start + startOffset;
        byte* end = start + endOffset;

        Vector3* currV = vPtr;
        Vector2* currVt = vtPtr;
        Vector3* currVn = vnPtr;
        int currentSortIdx = sortStartIndex;
        int sortEndIndex = sortStartIndex + sortCount;
        int localFaceIdx = 0;

        while (ptr < end)
        {
            while (ptr < end && *ptr <= 32) ptr++;
            if (ptr >= end) break;

            if (*ptr == '#')
            {
                while (ptr < end && *ptr != '\n') ptr++;
                continue;
            }

            byte c1 = *ptr;
            ptr++;
            if (ptr >= end) break;

            if (c1 == 'v')
            {
                byte c2 = *ptr;
                if (c2 == ' ' || c2 == '\t')
                {
                    *currV++ = new Vector3(ParseFloat(ref ptr, end), ParseFloat(ref ptr, end), ParseFloat(ref ptr, end));
                }
                else if (c2 == 't')
                {
                    ptr++;
                    *currVt++ = new Vector2(ParseFloat(ref ptr, end), 1.0f - ParseFloat(ref ptr, end));
                }
                else if (c2 == 'n')
                {
                    ptr++;
                    *currVn++ = new Vector3(ParseFloat(ref ptr, end), ParseFloat(ref ptr, end), ParseFloat(ref ptr, end));
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            else if (c1 == 'f')
            {
                if (*ptr <= 32)
                {
                    int definedV = vBase + (int)(currV - vPtr);
                    int definedVt = vtBase + (int)(currVt - vtPtr);
                    int definedVn = vnBase + (int)(currVn - vnPtr);

                    if (currentSortIdx + 3 <= sortEndIndex
                        && TryParseFaceVertex(ref ptr, end, out int v1, out int vt1, out int vn1)
                        && TryParseFaceVertex(ref ptr, end, out int v2, out int vt2, out int vn2)
                        && TryParseFaceVertex(ref ptr, end, out int v3, out int vt3, out int vn3))
                    {
                        v1 = ResolveIndex(v1, definedV); vt1 = ResolveIndex(vt1, definedVt); vn1 = ResolveIndex(vn1, definedVn);
                        v2 = ResolveIndex(v2, definedV); vt2 = ResolveIndex(vt2, definedVt); vn2 = ResolveIndex(vn2, definedVn);
                        v3 = ResolveIndex(v3, definedV); vt3 = ResolveIndex(vt3, definedVt); vn3 = ResolveIndex(vn3, definedVn);

                        sortArray[currentSortIdx] = new SortableVertex(v1, vt1, vn1, currentSortIdx);
                        currentSortIdx++;
                        sortArray[currentSortIdx] = new SortableVertex(v2, vt2, vn2, currentSortIdx);
                        currentSortIdx++;
                        sortArray[currentSortIdx] = new SortableVertex(v3, vt3, vn3, currentSortIdx);
                        currentSortIdx++;
                        localFaceIdx++;

                        while (currentSortIdx + 3 <= sortEndIndex)
                        {
                            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                            if (ptr >= end || *ptr == '\n') break;

                            v2 = v3; vt2 = vt3; vn2 = vn3;
                            if (!TryParseFaceVertex(ref ptr, end, out v3, out vt3, out vn3)) break;
                            v3 = ResolveIndex(v3, definedV); vt3 = ResolveIndex(vt3, definedVt); vn3 = ResolveIndex(vn3, definedVn);

                            sortArray[currentSortIdx] = new SortableVertex(v1, vt1, vn1, currentSortIdx);
                            currentSortIdx++;
                            sortArray[currentSortIdx] = new SortableVertex(v2, vt2, vn2, currentSortIdx);
                            currentSortIdx++;
                            sortArray[currentSortIdx] = new SortableVertex(v3, vt3, vn3, currentSortIdx);
                            currentSortIdx++;
                            localFaceIdx++;
                        }
                    }

                    while (ptr < end && *ptr != '\n') ptr++;
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            else if (c1 == 'm')
            {
                if (IsKeyword(ptr, end, "tllib"))
                {
                    ptr += 5;
                    while (ptr < end && *ptr != '\n')
                    {
                        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                        var s = ptr;
                        while (ptr < end && *ptr > 32 && *ptr != '\n') ptr++;
                        var len = (int)(ptr - s);
                        if (len > 0) result.MtlLibs.Add(Encoding.UTF8.GetString(s, len));
                    }
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            else if (c1 == 'o' || c1 == 'g' || c1 == 'u')
            {
                byte type = 0;
                if (c1 == 'o') type = 0;
                else if (c1 == 'g') type = 1;
                else if (c1 == 'u')
                {
                    if (IsKeyword(ptr, end, "semtl"))
                    {
                        ptr += 5;
                        type = 2;
                    }
                    else
                    {
                        while (ptr < end && *ptr != '\n') ptr++;
                        continue;
                    }
                }

                while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                var s = ptr;
                while (ptr < end && *ptr != '\n') ptr++;
                var len = (int)(ptr - s);
                if (len > 0)
                {
                    result.Events.Add(new SplitEvent { LocalFaceIndex = localFaceIdx, Type = type, Name = Encoding.UTF8.GetString(s, len).Trim() });
                }
            }
            else
            {
                while (ptr < end && *ptr != '\n') ptr++;
            }
        }

        while (currentSortIdx < sortEndIndex)
        {
            sortArray[currentSortIdx] = new SortableVertex(0, 0, 0, currentSortIdx);
            currentSortIdx++;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool TryParseFaceVertex(ref byte* ptr, byte* end, out int v, out int vt, out int vn)
    {
        v = 0;
        vt = 0;
        vn = 0;

        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
        if (ptr >= end || *ptr == '\n') return false;

        byte* tokenStart = ptr;
        v = ParseInt(ref ptr, end);

        if (ptr < end && *ptr == '/')
        {
            ptr++;
            if (ptr < end && *ptr != '/')
            {
                vt = ParseInt(ref ptr, end);
            }
            if (ptr < end && *ptr == '/')
            {
                ptr++;
                vn = ParseInt(ref ptr, end);
            }
        }

        if (ptr != tokenStart) return true;

        while (ptr < end && *ptr > 32) ptr++;
        return false;
    }

    private static unsafe bool IsKeyword(byte* ptr, byte* end, string keyword)
    {
        if (ptr + keyword.Length > end) return false;

        for (int i = 0; i < keyword.Length; i++)
        {
            if (*(ptr + i) != keyword[i]) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float ParseFloat(ref byte* ptr, byte* end)
    {
        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
        if (ptr >= end) return 0.0f;

        bool neg = false;
        if (*ptr == '-')
        {
            neg = true;
            ptr++;
        }
        else if (*ptr == '+')
        {
            ptr++;
        }

        long num = 0;
        long div = 1;
        bool decimalFound = false;

        while (ptr < end)
        {
            byte c = *ptr;
            if (c >= '0' && c <= '9')
            {
                num = num * 10 + (c - '0');
                if (decimalFound) div *= 10;
            }
            else if (c == '.')
            {
                decimalFound = true;
            }
            else if (c == 'e' || c == 'E')
            {
                ptr++;
                int exp = ParseInt(ref ptr, end);
                float baseVal = (float)num / div;
                return neg ? -baseVal * MathF.Pow(10, exp) : baseVal * MathF.Pow(10, exp);
            }
            else
            {
                break;
            }
            ptr++;
        }

        return neg ? (float)-num / div : (float)num / div;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int ParseInt(ref byte* ptr, byte* end)
    {
        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
        if (ptr >= end) return 0;

        bool neg = false;
        if (*ptr == '-')
        {
            neg = true;
            ptr++;
        }
        else if (*ptr == '+')
        {
            ptr++;
        }

        int num = 0;
        while (ptr < end)
        {
            byte c = *ptr;
            if (c >= '0' && c <= '9')
            {
                num = num * 10 + (c - '0');
            }
            else
            {
                break;
            }
            ptr++;
        }

        return neg ? -num : num;
    }
}
