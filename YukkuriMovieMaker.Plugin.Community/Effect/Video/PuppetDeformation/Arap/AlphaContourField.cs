using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// アルファ二値マスクから不透明領域の輪郭ポリラインを抽出する。
    /// 手順: 4連結ラベリング → ラベル保存1px膨張 → ピクセル境界追跡。
    /// 膨張はアンチエイリアスされた縁を輪郭の内側に確実に含めるためのマージンで、
    /// 異なる部位（別ラベル）に挟まれた透明ピクセルは膨張させないことで細い隙間の分離を保つ。
    /// 座標系はピクセル角座標（左上原点、(0,0)〜(width,height)）。
    /// </summary>
    internal sealed class AlphaContourField
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>膨張後の連結成分ラベル（0=透明）。輪郭の内側判定・内部点のラベル付けに使う</summary>
        public int[] Labels { get; }

        public List<Loop> Loops { get; }

        /// <summary>膨張後の不透明ピクセル数</summary>
        public int OpaquePixelCount { get; }

        /// <summary>
        /// 1本の閉じた輪郭。外周は符号付き面積が正、穴は負。
        /// </summary>
        public sealed class Loop
        {
            /// <summary>階段状輪郭の角（共線圧縮済み、ピクセル角座標、閉路だが終点は始点を重複させない）</summary>
            public required List<Vector2> Points { get; init; }
            /// <summary>輪郭が囲む連結成分のラベル</summary>
            public required int Label { get; init; }
            /// <summary>符号付き面積（正=外周、負=穴）</summary>
            public required double SignedArea { get; init; }
        }

        AlphaContourField(int width, int height, int[] labels, List<Loop> loops, int opaquePixelCount)
        {
            Width = width;
            Height = height;
            Labels = labels;
            Loops = loops;
            OpaquePixelCount = opaquePixelCount;
        }

        /// <summary>
        /// 不透明マスクから輪郭を抽出する。
        /// 境界エッジ数が上限を超える病的な入力（ノイズ画像など）ではnullを返す。
        /// </summary>
        public static AlphaContourField? TryBuild(bool[] opaque, int width, int height, int maxBoundaryEdges)
        {
            if (width <= 0 || height <= 0 || opaque.Length < width * height)
                return null;

            var labels = LabelComponents(opaque, width, height);
            DilatePreservingLabels(labels, width, height);

            var loops = TraceLoops(labels, width, height, maxBoundaryEdges, out var opaqueCount);
            if (loops is null)
                return null;

            return new AlphaContourField(width, height, labels, loops, opaqueCount);
        }

        /// <summary>4連結の連結成分ラベリング（2パスunion-find）。0=透明、1..=成分</summary>
        static int[] LabelComponents(bool[] opaque, int width, int height)
        {
            var labels = new int[width * height];
            var parent = new List<int> { 0 };

            int Find(int l)
            {
                var root = l;
                while (parent[root] != root)
                    root = parent[root];
                while (parent[l] != root)
                {
                    var next = parent[l];
                    parent[l] = root;
                    l = next;
                }
                return root;
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (!opaque[i])
                        continue;
                    var up = y > 0 ? labels[i - width] : 0;
                    var left = x > 0 ? labels[i - 1] : 0;
                    if (up == 0 && left == 0)
                    {
                        parent.Add(parent.Count);
                        labels[i] = parent.Count - 1;
                    }
                    else if (up == 0 || left == 0)
                    {
                        labels[i] = up | left;
                    }
                    else
                    {
                        var ru = Find(up);
                        var rl = Find(left);
                        var root = Math.Min(ru, rl);
                        parent[ru] = root;
                        parent[rl] = root;
                        labels[i] = root;
                    }
                }
            }

            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != 0)
                    labels[i] = Find(labels[i]);
            }
            return labels;
        }

        /// <summary>
        /// ラベル保存1px膨張。単一ラベルにのみ隣接（8近傍）する透明ピクセルを取り込む。
        /// ただし距離2以内（5x5）に異なるラベルが存在する場合は膨張させない。
        /// 8近傍だけの判定だと2pxの隙間が両側から埋まって異ラベル同士が隣接してしまうため、
        /// 5x5の安全判定により膨張後も異ラベル領域が決して隣接（8近傍）しないことを保証する。
        /// </summary>
        static void DilatePreservingLabels(int[] labels, int width, int height)
        {
            //取り込む対象は前景の境界リング分だけなので、遅延適用リストで済ませる（ラベル配列の複製を避ける）
            var additions = new List<(int Index, int Label)>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (labels[i] != 0)
                        continue;

                    //成長条件: 8近傍に前景ラベルがあること
                    var found = 0;
                    for (var dy = -1; dy <= 1 && found >= 0; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;
                            var nx = x + dx;
                            var ny = y + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                                continue;
                            var l = labels[ny * width + nx];
                            if (l == 0)
                                continue;
                            if (found == 0)
                            {
                                found = l;
                            }
                            else if (found != l)
                            {
                                found = -1;
                                break;
                            }
                        }
                    }
                    if (found <= 0)
                        continue;

                    //安全条件: 距離2以内に異なるラベルが無いこと
                    for (var dy = -2; dy <= 2 && found > 0; dy++)
                    {
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var nx = x + dx;
                            var ny = y + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                                continue;
                            var l = labels[ny * width + nx];
                            if (l != 0 && l != found)
                            {
                                found = -1;
                                break;
                            }
                        }
                    }
                    if (found > 0)
                        additions.Add((i, found));
                }
            }
            foreach (var (index, label) in additions)
                labels[index] = label;
        }

        //方向: 0=+x, 1=+y, 2=-x, 3=-y。輪郭は「進行方向の右側が不透明領域」になる向きに辿る
        static readonly (int dx, int dy)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

        /// <summary>
        /// 不透明ピクセル正方形の和集合の境界を追跡し、閉ループ群を返す。
        /// 対角接触の角（4エッジが集まる曖昧点）は右折優先で解決し、領域ごとにループを分離する。
        /// </summary>
        static List<Loop>? TraceLoops(int[] labels, int width, int height, int maxBoundaryEdges, out int opaqueCount)
        {
            opaqueCount = 0;
            var cornersX = width + 1;

            //境界エッジをキー: (角index << 2) | 方向 → ラベル で収集する
            var edges = new Dictionary<long, int>();
            var edgeKeys = new List<long>();

            static long EdgeKey(int cornerX, int cornerY, int dir, int cornersX)
                => (((long)cornerY * cornersX + cornerX) << 2) | (uint)dir;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var label = labels[y * width + x];
                    if (label == 0)
                        continue;
                    opaqueCount++;

                    //上下左右の透明側にエッジを張る（進行方向の右側が自ピクセル）
                    if (y == 0 || labels[(y - 1) * width + x] == 0)
                        AddEdge(EdgeKey(x, y, 0, cornersX), label);
                    if (x == width - 1 || labels[y * width + x + 1] == 0)
                        AddEdge(EdgeKey(x + 1, y, 1, cornersX), label);
                    if (y == height - 1 || labels[(y + 1) * width + x] == 0)
                        AddEdge(EdgeKey(x + 1, y + 1, 2, cornersX), label);
                    if (x == 0 || labels[y * width + x - 1] == 0)
                        AddEdge(EdgeKey(x, y + 1, 3, cornersX), label);

                    if (edges.Count > maxBoundaryEdges)
                        return null;
                }
            }

            void AddEdge(long key, int label)
            {
                edges.Add(key, label);
                edgeKeys.Add(key);
            }

            var loops = new List<Loop>();
            foreach (var startKey in edgeKeys)
            {
                if (!edges.TryGetValue(startKey, out var label))
                    continue;

                var points = new List<Vector2>();
                var curKey = startKey;
                var closed = false;
                //ループ長の上限は総エッジ数（安全のための無限ループガード）
                for (var guard = 0; guard <= edgeKeys.Count; guard++)
                {
                    var corner = curKey >> 2;
                    var dir = (int)(curKey & 3);
                    edges.Remove(curKey);

                    var (dx, dy) = Directions[dir];
                    var nextX = (int)(corner % cornersX) + dx;
                    var nextY = (int)(corner / cornersX) + dy;

                    //右折→直進→左折→反転の優先順で次のエッジを選ぶ（右折優先が対角接触の領域分離を保つ）
                    var nextKey = -1L;
                    var nextDir = -1;
                    ReadOnlySpan<int> candidates = [(dir + 1) & 3, dir, (dir + 3) & 3, (dir + 2) & 3];
                    foreach (var cand in candidates)
                    {
                        var k = EdgeKey(nextX, nextY, cand, cornersX);
                        //始点エッジは消費済みだがキー照合で閉路を検出する
                        if (k == startKey || edges.ContainsKey(k))
                        {
                            nextKey = k;
                            nextDir = cand;
                            break;
                        }
                    }
                    if (nextKey < 0)
                        break;

                    //向きが変わる角だけを頂点として採る（共線圧縮）
                    if (nextDir != dir)
                        points.Add(new Vector2(nextX, nextY));

                    if (nextKey == startKey)
                    {
                        closed = true;
                        break;
                    }
                    curKey = nextKey;
                }

                if (!closed || points.Count < 3)
                    continue;

                loops.Add(new Loop
                {
                    Points = points,
                    Label = label,
                    SignedArea = SignedArea(points),
                });
            }
            return loops;
        }

        static double SignedArea(List<Vector2> points)
        {
            var area = 0.0;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += (double)a.X * b.Y - (double)b.X * a.Y;
            }
            return area * 0.5;
        }
    }
}
