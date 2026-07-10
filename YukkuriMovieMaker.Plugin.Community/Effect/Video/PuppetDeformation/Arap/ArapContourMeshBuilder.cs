using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// アルファマスクから輪郭に沿ったARAP用三角形メッシュを構築する。
    /// パイプライン: 輪郭抽出 → Douglas-Peucker簡略化＋辺長制限 → 内部ヘックス点 →
    /// Bowyer-Watson Delaunay → 輪郭辺の中点分割によるconforming化 → 外側三角形の除去 →
    /// yバケット順の頂点番号付け（BandCholesky用の帯幅保証）。
    /// 三角形予算を超える場合は簡略化を粗くして再試行し、それでも失敗したらnullを返す
    /// （呼び出し側はグリッドメッシュへフォールバックする）。
    /// 全工程が決定論的で、同一入力からは同一メッシュが得られる。
    /// </summary>
    internal static class ArapContourMeshBuilder
    {
        //境界エッジ数の上限（ノイズ状アルファの病的入力はグリッドへフォールバック）
        const int MaxBoundaryEdges = 1_000_000;
        //Douglas-Peuckerの初期許容誤差(px)。輪郭は膨張済みなので1px削れても元ピクセルを切らない
        const float InitialSimplifyEpsilon = 1f;
        //辺の最大長のspacing倍率。帯幅の上界・輪郭上の変形自由度・conforming収束を同時に保証する
        const float MaxEdgeScale = 1.5f;
        //内部点が輪郭セグメントから保つ距離のspacing倍率
        const float ContourClearanceScale = 0.7f;
        //ヘックス格子の行間隔係数（√3/2）
        const float HexRowScale = 0.8660254f;
        //正三角形近似での「面積→三角形数」係数（4/√3）
        const float TrianglesPerArea = 2.31f;
        //conforming化（中点分割）の最大パス数
        const int MaxConformingPasses = 16;
        //これ以上短い制約セグメントは分割しない（発散ガード。1px未満の制約違反は容認する）
        const float MinConstraintLength = 1f;
        //yバケット順で保証できなかった場合の帯幅上限（超えたら再試行→フォールバック）
        const int MaxSolverBandwidth = 512;

        /// <summary>
        /// アルファマスクから輪郭メッシュを構築する。構築できない場合はnull。
        /// </summary>
        /// <param name="opaque">不透明ピクセルのマスク（maskWidth×maskHeight、行優先）</param>
        /// <param name="maskWidth">マスクの幅(px)</param>
        /// <param name="maskHeight">マスクの高さ(px)</param>
        /// <param name="width">画像のローカル幅（メッシュ座標変換に使う）</param>
        /// <param name="height">画像のローカル高さ</param>
        /// <param name="maxTriangles">三角形数の上限</param>
        /// <param name="minSpacing">内部点間隔の下限(px)</param>
        public static ArapContourMesh? TryBuild(
            bool[] opaque, int maskWidth, int maskHeight,
            float width, float height,
            int maxTriangles, float minSpacing)
        {
            var field = AlphaContourField.TryBuild(opaque, maskWidth, maskHeight, MaxBoundaryEdges);
            if (field is null || field.Loops.Count == 0 || field.OpaquePixelCount == 0)
                return null;

            //予算超過時は簡略化を粗く・間隔を広くして再試行する
            var epsilon = InitialSimplifyEpsilon;
            var spacingFloor = minSpacing;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var mesh = TryBuildOnce(field, width, height, maxTriangles, spacingFloor, epsilon, out var usedSpacing);
                if (mesh is not null)
                    return mesh;
                epsilon *= 1.5f;
                //εを粗くすると輪郭点数が減って予算由来のspacingが縮み得るが、
                //FillSmallHoleによるラベル格子の書き換えは巻き戻せないため、
                //spacingは試行間で単調非減少にし「前の試行で埋めた穴を後の試行が制約ループとして残す」矛盾を防ぐ
                spacingFloor = MathF.Max(spacingFloor * 1.25f, usedSpacing);
            }
            return null;
        }

        static ArapContourMesh? TryBuildOnce(
            AlphaContourField field,
            float width, float height,
            int maxTriangles, float spacingFloor, float epsilon,
            out float usedSpacing)
        {
            usedSpacing = spacingFloor;
            //1. 輪郭をDouglas-Peuckerで簡略化する
            var simplified = new List<(List<Vector2> Points, int Label, double SignedArea)>(field.Loops.Count);
            foreach (var loop in field.Loops)
                simplified.Add((SimplifyClosedLoop(loop.Points, epsilon), loop.Label, loop.SignedArea));

            //2. 輪郭点数から内部点間隔を決める（境界帯に約3点/輪郭点分の三角形を予約）
            var boundaryPointCount = simplified.Sum(l => l.Points.Count);
            if (boundaryPointCount * 3 > maxTriangles / 2)
                return null;
            var spacing = MathF.Max(
                spacingFloor,
                MathF.Sqrt(TrianglesPerArea * field.OpaquePixelCount / Math.Max(1, maxTriangles - boundaryPointCount * 3)));
            usedSpacing = spacing;
            var maxEdge = spacing * MaxEdgeScale;

            //3. 小さな穴は捨てる（メッシュで覆っても透明テクセルは透明に描かれるため視覚的に無損失）。
            //   小さな島（外周）は描画から消えてしまうため保持する。
            //   捨てた穴はラベル格子上で埋めておき、分類の高速パス（ラベル参照）でも内側扱いになるようにする
            for (var i = simplified.Count - 1; i >= 0; i--)
            {
                var (points, label, signedArea) = simplified[i];
                if (signedArea >= 0 || -signedArea >= spacing * spacing * 0.5)
                    continue;
                FillSmallHole(field, points, label);
                simplified.RemoveAt(i);
            }
            if (simplified.Count == 0)
                return null;

            //4. 辺長を制限しつつ制約点・制約セグメントを作る。
            //   対角接触で複数ループが共有する角は同一頂点に統合する（ラベルが異なる場合はワイルドカード0）
            var pointMap = new Dictionary<(float, float), int>();
            var rawPoints = new List<Vector2>();
            var pointLabels = new List<int>();
            var segments = new List<(int A, int B)>();

            int AddConstraintPoint(Vector2 p, int label)
            {
                if (pointMap.TryGetValue((p.X, p.Y), out var existing))
                {
                    if (pointLabels[existing] != label)
                        pointLabels[existing] = 0;
                    return existing;
                }
                var index = rawPoints.Count;
                pointMap[(p.X, p.Y)] = index;
                rawPoints.Add(p);
                pointLabels.Add(label);
                return index;
            }

            foreach (var (points, label, _) in simplified)
            {
                var subdivided = SubdivideLoop(points, maxEdge);
                var indices = new int[subdivided.Count];
                for (var i = 0; i < subdivided.Count; i++)
                    indices[i] = AddConstraintPoint(subdivided[i], label);
                for (var i = 0; i < indices.Length; i++)
                {
                    var a = indices[i];
                    var b = indices[(i + 1) % indices.Length];
                    if (a != b)
                        segments.Add((a, b));
                }
            }
            if (rawPoints.Count < 3)
                return null;

            //5. 内部にヘックス格子点を撒く（輪郭の内側かつ境界からclearance以上。距離場のO(1)参照で判定）
            var interiorPoints = BuildInteriorPoints(rawPoints, field, spacing);

            //6. Delaunay三角形分割（全点に決定論的ジッタを掛けて退化を避ける）
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in rawPoints)
            {
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
            }
            var triangulator = new DelaunayTriangulator(new Vector2(minX, minY), new Vector2(maxX, maxY));

            //Delaunay頂点index → ラベル。スーパートライアングル頂点はワイルドカード
            var vertexLabels = new List<int> { 0, 0, 0 };

            var constraintIndexMap = new int[rawPoints.Count];
            for (var i = 0; i < rawPoints.Count; i++)
            {
                var index = triangulator.InsertPoint(Jitter(rawPoints[i]));
                if (index < 0)
                    return null; //制約点が挿入できない場合はconformingが成立しないため再試行へ
                constraintIndexMap[i] = index;
                vertexLabels.Add(pointLabels[i]);
            }
            foreach (var (p, label) in interiorPoints)
            {
                if (triangulator.InsertPoint(Jitter(p)) >= 0)
                    vertexLabels.Add(label);
            }

            //7. conforming化: 輪郭セグメントがDelaunay辺として存在するまで中点分割を繰り返す
            var constraintSegments = new List<(int A, int B)>(segments.Count);
            foreach (var (a, b) in segments)
                constraintSegments.Add((constraintIndexMap[a], constraintIndexMap[b]));

            for (var pass = 0; pass < MaxConformingPasses; pass++)
            {
                var edgeSet = triangulator.BuildUndirectedEdgeSet();
                var next = new List<(int A, int B)>(constraintSegments.Count);
                var changed = false;
                foreach (var (a, b) in constraintSegments)
                {
                    if (edgeSet.Contains(DelaunayTriangulator.UndirectedEdgeKey(a, b)))
                    {
                        next.Add((a, b));
                        continue;
                    }
                    var pa = triangulator.GetPoint(a);
                    var pb = triangulator.GetPoint(b);
                    if (Vector2.Distance(pa, pb) < MinConstraintLength)
                    {
                        //これ以上分割しない（1px未満の制約違反は容認。跨ぎ三角形はラベル規則が除去する）
                        next.Add((a, b));
                        continue;
                    }
                    var mid = triangulator.InsertPoint(Jitter((pa + pb) * 0.5f));
                    if (mid < 0)
                    {
                        next.Add((a, b));
                        continue;
                    }
                    var la = vertexLabels[a];
                    var lb = vertexLabels[b];
                    vertexLabels.Add(la == lb ? la : 0);
                    next.Add((a, mid));
                    next.Add((mid, b));
                    changed = true;
                }
                constraintSegments = next;
                if (!changed)
                    break;
            }

            //8. 外側の三角形を除去する。
            //   重心の内外判定は、境界から十分離れた重心なら距離場＋ラベル参照で即決し（大半がこちら）、
            //   境界すれすれの重心だけ制約ポリゴンとの偶奇判定で精密に行う
            //   （簡略化ポリゴンとピクセル境界のずれ ≦ ε による縁の誤判定を防ぐため）。
            //   加えて、異なる部位のラベルを跨ぐ三角形は無条件に除去する
            //   （細い隙間を跨いだ微小三角形による部位の溶接を防ぐ）
            var polygonSegments = new List<(Vector2 A, Vector2 B)>(constraintSegments.Count);
            foreach (var (a, b) in constraintSegments)
                polygonSegments.Add((triangulator.GetPoint(a), triangulator.GetPoint(b)));

            //ポリゴンのずれ(ε) + 重心のピクセル量子化(≈1.2px) + 距離場が膨張前基準であるずれ(1px)
            //を上回る余白を持たせる（chamfer値3≒1px）
            var fastPathThreshold = (int)((epsilon + 3.5f) * 3f);
            var kept = new List<(int A, int B, int C)>();
            foreach (var (a, b, c) in triangulator.GetTriangles())
            {
                if (SpansDifferentLabels(vertexLabels[a], vertexLabels[b], vertexLabels[c]))
                    continue;
                var centroid = (triangulator.GetPoint(a) + triangulator.GetPoint(b) + triangulator.GetPoint(c)) / 3f;
                var px = Math.Clamp((int)MathF.Floor(centroid.X), 0, field.Width - 1);
                var py = Math.Clamp((int)MathF.Floor(centroid.Y), 0, field.Height - 1);
                var pixel = py * field.Width + px;
                bool inside;
                if (field.BoundaryDistance[pixel] >= fastPathThreshold)
                    inside = field.Labels[pixel] != 0;
                else
                    inside = IsInsidePolygons(centroid, polygonSegments);
                if (!inside)
                    continue;
                kept.Add((a, b, c));
            }
            if (kept.Count == 0 || kept.Count > maxTriangles)
                return null;

            //9. 使用頂点を圧縮し、yバケット順（バケット内はx順）に番号を振り直す。
            //   辺長 ≦ maxEdge の保証により、番号差＝帯幅が「高さmaxEdgeの帯内の頂点数」程度に収まる
            var used = new HashSet<int>();
            foreach (var (a, b, c) in kept)
            {
                used.Add(a);
                used.Add(b);
                used.Add(c);
            }
            var order = used
                .OrderBy(i => (int)MathF.Floor(triangulator.GetPoint(i).Y / spacing))
                .ThenBy(i => triangulator.GetPoint(i).X)
                .ThenBy(i => triangulator.GetPoint(i).Y)
                .ToArray();
            var newIndex = new Dictionary<int, int>(order.Length);
            for (var i = 0; i < order.Length; i++)
                newIndex[order[i]] = i;

            //ピクセル角座標 → 画像中央原点のローカル座標へ変換する
            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            var restPositions = new Vector2[order.Length];
            for (var i = 0; i < order.Length; i++)
            {
                var p = triangulator.GetPoint(order[i]);
                restPositions[i] = new Vector2(p.X - halfW, p.Y - halfH);
            }

            var triangleIndices = new int[kept.Count * 3];
            var bandwidth = 1;
            for (var t = 0; t < kept.Count; t++)
            {
                var (a, b, c) = kept[t];
                var i0 = newIndex[a];
                var i1 = newIndex[b];
                var i2 = newIndex[c];
                triangleIndices[t * 3 + 0] = i0;
                triangleIndices[t * 3 + 1] = i1;
                triangleIndices[t * 3 + 2] = i2;
                bandwidth = Math.Max(bandwidth, Math.Abs(i0 - i1));
                bandwidth = Math.Max(bandwidth, Math.Abs(i1 - i2));
                bandwidth = Math.Max(bandwidth, Math.Abs(i2 - i0));
            }
            if (bandwidth > MaxSolverBandwidth)
                return null;

            return new ArapContourMesh(width, height, restPositions, triangleIndices, bandwidth, spacing);
        }

        static bool SpansDifferentLabels(int la, int lb, int lc)
        {
            //0はワイルドカード（共有角・不明）として無視する
            var first = la != 0 ? la : lb != 0 ? lb : lc;
            if (first == 0)
                return false;
            return (la != 0 && la != first) || (lb != 0 && lb != first) || (lc != 0 && lc != first);
        }

        /// <summary>
        /// 閉ループをDouglas-Peuckerで簡略化する。最遠点対をアンカーに2本の開チェーンへ分割して適用する。
        /// 簡略化で3点未満に潰れる場合（1px島など）は元の点列を返す。
        /// </summary>
        static List<Vector2> SimplifyClosedLoop(List<Vector2> points, float epsilon)
        {
            if (points.Count <= 4)
                return points;

            var anchor0 = FarthestIndexFrom(points, points[0]);
            var anchor1 = FarthestIndexFrom(points, points[anchor0]);
            if (anchor0 == anchor1)
                return points;
            if (anchor0 > anchor1)
                (anchor0, anchor1) = (anchor1, anchor0);

            var keep = new bool[points.Count];
            keep[anchor0] = true;
            keep[anchor1] = true;
            DouglasPeucker(points, anchor0, anchor1, epsilon, keep, wrap: false);
            DouglasPeucker(points, anchor1, anchor0 + points.Count, epsilon, keep, wrap: true);

            var result = new List<Vector2>();
            for (var i = 0; i < points.Count; i++)
            {
                if (keep[i])
                    result.Add(points[i]);
            }
            return result.Count >= 3 ? result : points;
        }

        static int FarthestIndexFrom(List<Vector2> points, Vector2 origin)
        {
            var best = 0;
            var bestDistSq = float.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                var distSq = Vector2.DistanceSquared(points[i], origin);
                if (distSq > bestDistSq)
                {
                    bestDistSq = distSq;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 反復版Douglas-Peucker。startからend（wrap時はインデックスを点数で剰余）までのチェーンを処理し、
        /// 残す点をkeepに立てる。最大逸脱が許容誤差以下の中間点は落とされる。
        /// </summary>
        static void DouglasPeucker(List<Vector2> points, int start, int end, float epsilon, bool[] keep, bool wrap)
        {
            var n = points.Count;
            var stack = new Stack<(int Start, int End)>();
            stack.Push((start, end));
            while (stack.Count > 0)
            {
                var (s, e) = stack.Pop();
                if (e - s < 2)
                    continue;
                var a = points[wrap ? s % n : s];
                var b = points[wrap ? e % n : e];
                var worst = -1;
                var worstDistSq = (float)(epsilon * (double)epsilon);
                for (var i = s + 1; i < e; i++)
                {
                    var distSq = DistanceToSegmentSquared(points[wrap ? i % n : i], a, b);
                    if (distSq > worstDistSq)
                    {
                        worstDistSq = distSq;
                        worst = i;
                    }
                }
                if (worst < 0)
                    continue;
                keep[wrap ? worst % n : worst] = true;
                stack.Push((s, worst));
                stack.Push((worst, e));
            }
        }

        static float DistanceToSegmentSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.LengthSquared();
            if (lengthSq < 1e-12f)
                return Vector2.DistanceSquared(p, a);
            var t = Math.Clamp(Vector2.Dot(p - a, ab) / lengthSq, 0f, 1f);
            return Vector2.DistanceSquared(p, a + ab * t);
        }

        /// <summary>辺長がmaxEdgeを超えないよう等分割した閉ループ点列を返す</summary>
        static List<Vector2> SubdivideLoop(List<Vector2> points, float maxEdge)
        {
            var result = new List<Vector2>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                result.Add(a);
                var divisions = (int)MathF.Ceiling(Vector2.Distance(a, b) / maxEdge);
                for (var k = 1; k < divisions; k++)
                    result.Add(Vector2.Lerp(a, b, (float)k / divisions));
            }
            return result;
        }

        /// <summary>
        /// 輪郭の内側にヘックス格子の内部点を撒く。
        /// 内外と輪郭からのクリアランスは距離場＋ラベル格子のO(1)参照で判定する
        /// （クリアランス ≧ 0.7h は境界のε誤差より十分大きいため、ピクセル境界基準の距離で問題ない）。
        /// 各点には所属する連結成分のラベルを付ける。
        /// </summary>
        static List<(Vector2 Point, int Label)> BuildInteriorPoints(
            List<Vector2> constraintPoints,
            AlphaContourField field,
            float spacing)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in constraintPoints)
            {
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
            }

            //chamfer距離の値3が約1pxに相当する
            var minChamfer = (int)(spacing * ContourClearanceScale * 3f);
            var rowStep = spacing * HexRowScale;
            var result = new List<(Vector2, int)>();
            var row = 0;
            for (var y = minY + rowStep * 0.5f; y < maxY; y += rowStep, row++)
            {
                var xOffset = (row & 1) == 0 ? spacing * 0.5f : spacing;
                for (var x = minX + xOffset; x < maxX; x += spacing)
                {
                    var px = Math.Clamp((int)MathF.Floor(x), 0, field.Width - 1);
                    var py = Math.Clamp((int)MathF.Floor(y), 0, field.Height - 1);
                    var pixel = py * field.Width + px;
                    var label = field.Labels[pixel];
                    if (label == 0 || field.BoundaryDistance[pixel] < minChamfer)
                        continue;
                    result.Add((new Vector2(x, y), label));
                }
            }
            return result;
        }

        /// <summary>
        /// 捨てた小さな穴の内部をラベル格子上で埋める（穴の輪郭ポリゴンをスキャンライン充填）。
        /// 距離場は更新しない: 穴の周囲は距離が小さいままなので分類は精密パスに落ち、正しく内側と判定される。
        /// </summary>
        static void FillSmallHole(AlphaContourField field, List<Vector2> polygon, int label)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in polygon)
            {
                minX = MathF.Min(minX, p.X);
                minY = MathF.Min(minY, p.Y);
                maxX = MathF.Max(maxX, p.X);
                maxY = MathF.Max(maxY, p.Y);
            }
            var y0 = Math.Max(0, (int)MathF.Floor(minY));
            var y1 = Math.Min(field.Height - 1, (int)MathF.Ceiling(maxY));
            var x0 = Math.Max(0, (int)MathF.Floor(minX));
            var x1 = Math.Min(field.Width - 1, (int)MathF.Ceiling(maxX));
            var crossings = new List<float>();
            for (var y = y0; y <= y1; y++)
            {
                var cy = y + 0.5f;
                crossings.Clear();
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    if ((a.Y <= cy) == (b.Y <= cy))
                        continue;
                    crossings.Add(a.X + (cy - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
                crossings.Sort();
                for (var c = 0; c + 1 < crossings.Count; c += 2)
                {
                    var sx = Math.Max(x0, (int)MathF.Ceiling(crossings[c] - 0.5f));
                    var ex = Math.Min(x1, (int)MathF.Floor(crossings[c + 1] - 0.5f));
                    for (var x = sx; x <= ex; x++)
                    {
                        var pixel = y * field.Width + x;
                        if (field.Labels[pixel] == 0)
                            field.Labels[pixel] = label;
                    }
                }
            }
        }

        /// <summary>偶奇規則による内外判定（+x方向レイキャスト）</summary>
        static bool IsInsidePolygons(Vector2 p, List<(Vector2 A, Vector2 B)> segments)
        {
            var crossings = 0;
            foreach (var (a, b) in segments)
            {
                if ((a.Y <= p.Y) == (b.Y <= p.Y))
                    continue;
                var x = a.X + (p.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X);
                if (x > p.X)
                    crossings++;
            }
            return (crossings & 1) == 1;
        }

        /// <summary>
        /// 座標ビットのハッシュによる決定論的な±0.001pxジッタ。
        /// 格子・整数座標由来の共円/共線退化を避ける。同一入力からは常に同一メッシュになる
        /// </summary>
        static Vector2 Jitter(Vector2 p)
        {
            var bx = BitConverter.SingleToUInt32Bits(p.X);
            var by = BitConverter.SingleToUInt32Bits(p.Y);
            var hx = Hash(bx, by, 0x9E3779B9u);
            var hy = Hash(bx, by, 0x85EBCA6Bu);
            return new Vector2(
                p.X + ((hx & 0xFFFF) / 65535f - 0.5f) * 0.002f,
                p.Y + ((hy & 0xFFFF) / 65535f - 0.5f) * 0.002f);
        }

        static uint Hash(uint a, uint b, uint seed)
        {
            var h = seed;
            h ^= a * 0xCC9E2D51u;
            h = (h << 15) | (h >> 17);
            h *= 0x1B873593u;
            h ^= b * 0xCC9E2D51u;
            h = (h << 13) | (h >> 19);
            h = h * 5u + 0xE6546B64u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}
