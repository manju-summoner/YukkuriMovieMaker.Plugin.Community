using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// As-Rigid-As-Possible (Sorkine-Alexa 2007) の2D local-globalソルバー。
    /// メッシュとピンのレスト位置が変わらない限り行列分解を再利用でき、
    /// 毎フレームはピン目標位置からの反復（局所回転推定＋バンド前進/後退代入）のみで済む。
    /// 反復回数固定＋毎フレームMLS初期化のため、結果はフレーム間の履歴に依存しない。
    /// </summary>
    internal sealed class ArapDeformer
    {
        const double PinWeightScale = 1e4;
        const double IdentityEpsilon = 1e-6;

        readonly ArapGridMesh mesh;
        readonly Vector2[] pinRests;
        readonly BandCholesky cholesky;

        //隣接リスト(CSR形式)
        readonly int[] neighborStart;
        readonly int[] neighborIndex;
        readonly double[] neighborWeight;

        //ピン拘束: 所属三角形の3頂点と重心座標
        readonly (int V0, int V1, int V2, double B0, double B1, double B2)[] pinAttachments;
        //各ピンのアタッチ点のレスト位置。画像外や除去済み三角形上のピンはレスト位置と一致しないため、
        //拘束目標は「アタッチ点レスト + ピンの移動量」として相対的に適用する
        readonly Vector2[] pinAttachRests;
        readonly double pinWeight;

        //エッジを持たない頂点（マスクで除去された領域）。対角1の恒等行としてレスト位置に固定する
        readonly bool[] isIsolated;
        //ピンの無い連結成分の頂点。動かしようがないためレスト位置にアンカーする
        readonly bool[] isAnchored;

        //作業バッファ（Solve間で再利用）
        readonly double[] qx, qy, bx, by, rotCos, rotSin;

        public int VertexCount => mesh.VertexCount;

        ArapDeformer(
            ArapGridMesh mesh,
            Vector2[] pinRests,
            BandCholesky cholesky,
            int[] neighborStart,
            int[] neighborIndex,
            double[] neighborWeight,
            (int, int, int, double, double, double)[] pinAttachments,
            Vector2[] pinAttachRests,
            double pinWeight,
            bool[] isIsolated,
            bool[] isAnchored)
        {
            this.mesh = mesh;
            this.pinRests = pinRests;
            this.cholesky = cholesky;
            this.neighborStart = neighborStart;
            this.neighborIndex = neighborIndex;
            this.neighborWeight = neighborWeight;
            this.pinAttachments = pinAttachments;
            this.pinAttachRests = pinAttachRests;
            this.pinWeight = pinWeight;
            this.isIsolated = isIsolated;
            this.isAnchored = isAnchored;

            var n = mesh.VertexCount;
            qx = new double[n];
            qy = new double[n];
            bx = new double[n];
            by = new double[n];
            rotCos = new double[n];
            rotSin = new double[n];
        }

        /// <summary>
        /// メッシュとピンのレスト位置から行列を組み立てて分解する。
        /// 分解に失敗した場合（拘束なしで特異な場合など）はnullを返す。
        /// </summary>
        public static ArapDeformer? TryCreate(ArapGridMesh mesh, IReadOnlyList<Vector2> pinRestPositions)
        {
            if (pinRestPositions.Count == 0)
                return null;

            var n = mesh.VertexCount;

            //エッジ配列から隣接CSRを構築
            var counts = new int[n];
            foreach (var (a, b, _) in mesh.Edges)
            {
                counts[a]++;
                counts[b]++;
            }
            var neighborStart = new int[n + 1];
            for (var i = 0; i < n; i++)
                neighborStart[i + 1] = neighborStart[i] + counts[i];
            var neighborIndex = new int[neighborStart[n]];
            var neighborWeight = new double[neighborStart[n]];
            var cursor = new int[n];
            foreach (var (a, b, w) in mesh.Edges)
            {
                var ia = neighborStart[a] + cursor[a]++;
                neighborIndex[ia] = b;
                neighborWeight[ia] = w;
                var ib = neighborStart[b] + cursor[b]++;
                neighborIndex[ib] = a;
                neighborWeight[ib] = w;
            }

            //バンド幅: グリッド隣接（±(cellsX+2)）とピン拘束の三角形内結合が収まる幅
            var bandwidth = mesh.CellsX + 2;
            var cholesky = new BandCholesky(n, bandwidth);

            //ラプラシアン部分: L_ii = Σw, L_ij = -w。
            //マスクでエッジを失った孤立頂点は対角1の恒等行にして帯構造と正定値性を保つ
            var isIsolated = new bool[n];
            var diagSum = 0.0;
            var connectedCount = 0;
            for (var i = 0; i < n; i++)
            {
                if (neighborStart[i] == neighborStart[i + 1])
                {
                    isIsolated[i] = true;
                    cholesky.Add(i, i, 1.0);
                    continue;
                }
                var sum = 0.0;
                for (var k = neighborStart[i]; k < neighborStart[i + 1]; k++)
                {
                    sum += neighborWeight[k];
                    if (neighborIndex[k] < i)
                        cholesky.Add(i, neighborIndex[k], -neighborWeight[k]);
                }
                cholesky.Add(i, i, sum);
                diagSum += sum;
                connectedCount++;
            }
            if (connectedCount == 0)
                return null;

            //ピン拘束: λ |B q - t|^2 → 行列に λ b_k b_l を加算
            var pinWeight = PinWeightScale * Math.Max(1.0, diagSum / connectedCount);
            var attachments = new (int, int, int, double, double, double)[pinRestPositions.Count];
            var attachRests = new Vector2[pinRestPositions.Count];
            for (var p = 0; p < pinRestPositions.Count; p++)
            {
                var (v0, v1, v2, b0, b1, b2) = mesh.FindContainingTriangle(pinRestPositions[p]);
                attachments[p] = (v0, v1, v2, b0, b1, b2);
                attachRests[p] =
                    mesh.RestPositions[v0] * (float)b0 +
                    mesh.RestPositions[v1] * (float)b1 +
                    mesh.RestPositions[v2] * (float)b2;

                Span<int> verts = [v0, v1, v2];
                Span<double> bary = [b0, b1, b2];
                for (var k = 0; k < 3; k++)
                    for (var l = 0; l <= k; l++)
                    {
                        if (verts[k] == verts[l] && k != l)
                            continue; //同一頂点の重複はAddで二重加算しない（グリッドでは発生しないが保険）
                        cholesky.Add(verts[k], verts[l], pinWeight * bary[k] * bary[l]);
                    }
            }

            //ピンが1つも乗っていない連結成分は並進の自由度が残り特異になるため、レスト位置にアンカーする
            var isAnchored = FindPinlessComponentVertices(n, neighborStart, neighborIndex, attachments, isIsolated);
            for (var i = 0; i < n; i++)
            {
                if (isAnchored[i])
                    cholesky.Add(i, i, pinWeight);
            }

            if (!cholesky.Factorize())
                return null;

            var rests = new Vector2[pinRestPositions.Count];
            for (var i = 0; i < rests.Length; i++)
                rests[i] = pinRestPositions[i];

            return new ArapDeformer(mesh, rests, cholesky, neighborStart, neighborIndex, neighborWeight, attachments, attachRests, pinWeight, isIsolated, isAnchored);
        }

        /// <summary>
        /// ピン目標位置からメッシュ頂点の変形後位置を計算する。
        /// </summary>
        /// <param name="pinTargets">各ピンの目標位置（TryCreateに渡したレスト位置と同数・同順）</param>
        /// <param name="iterations">local-global反復回数</param>
        /// <param name="result">頂点数分の出力バッファ</param>
        public void Solve(IReadOnlyList<Vector2> pinTargets, int iterations, Vector2[] result)
        {
            if (pinTargets.Count != pinRests.Length)
                throw new ArgumentException("ピン数がTryCreate時と一致していません", nameof(pinTargets));
            if (result.Length < mesh.VertexCount)
                throw new ArgumentException("出力バッファが不足しています", nameof(result));

            var rests = mesh.RestPositions;
            var n = mesh.VertexCount;

            //全ピンがレスト位置のままなら恒等変形
            var isIdentity = true;
            for (var p = 0; p < pinTargets.Count; p++)
            {
                if (Math.Abs(pinTargets[p].X - pinRests[p].X) > IdentityEpsilon ||
                    Math.Abs(pinTargets[p].Y - pinRests[p].Y) > IdentityEpsilon)
                {
                    isIdentity = false;
                    break;
                }
            }
            if (isIdentity)
            {
                Array.Copy(rests, result, n);
                return;
            }

            //決定論的な初期値: 毎フレーム forward rigid-MLS から開始する（履歴非依存）
            for (var i = 0; i < n; i++)
            {
                var d = ForwardRigidMls(rests[i], pinTargets);
                qx[i] = d.X;
                qy[i] = d.Y;
            }

            for (var it = 0; it < iterations; it++)
            {
                //local step: 各頂点の最適回転を求める
                for (var i = 0; i < n; i++)
                {
                    double m00 = 0, m01 = 0, m10 = 0, m11 = 0;
                    var px = (double)rests[i].X;
                    var py = (double)rests[i].Y;
                    for (var k = neighborStart[i]; k < neighborStart[i + 1]; k++)
                    {
                        var j = neighborIndex[k];
                        var w = neighborWeight[k];
                        var pjx = px - rests[j].X;
                        var pjy = py - rests[j].Y;
                        var qjx = qx[i] - qx[j];
                        var qjy = qy[i] - qy[j];
                        m00 += w * qjx * pjx;
                        m01 += w * qjx * pjy;
                        m10 += w * qjy * pjx;
                        m11 += w * qjy * pjy;
                    }
                    var c = m00 + m11;
                    var s = m10 - m01;
                    var r = Math.Sqrt(c * c + s * s);
                    if (r < 1e-12)
                    {
                        rotCos[i] = 1;
                        rotSin[i] = 0;
                    }
                    else
                    {
                        rotCos[i] = c / r;
                        rotSin[i] = s / r;
                    }
                }

                //global step: Σw(q_i - q_j) = Σ(w/2)(R_i + R_j)(p_i - p_j) + ピン項 を解く
                Array.Clear(bx, 0, n);
                Array.Clear(by, 0, n);
                for (var i = 0; i < n; i++)
                {
                    var px = (double)rests[i].X;
                    var py = (double)rests[i].Y;
                    double sx = 0, sy = 0;
                    for (var k = neighborStart[i]; k < neighborStart[i + 1]; k++)
                    {
                        var j = neighborIndex[k];
                        var w = neighborWeight[k] * 0.5;
                        var pjx = px - rests[j].X;
                        var pjy = py - rests[j].Y;
                        var rc = rotCos[i] + rotCos[j];
                        var rs = rotSin[i] + rotSin[j];
                        sx += w * (rc * pjx - rs * pjy);
                        sy += w * (rs * pjx + rc * pjy);
                    }
                    bx[i] = sx;
                    by[i] = sy;
                }
                for (var p = 0; p < pinAttachments.Length; p++)
                {
                    var (v0, v1, v2, b0, b1, b2) = pinAttachments[p];
                    //アタッチ点をピンの移動量だけ動かす（画像外ピンのクランプ位置へのテレポートを防ぐ）
                    var tx = pinAttachRests[p].X + (double)(pinTargets[p].X - pinRests[p].X);
                    var ty = pinAttachRests[p].Y + (double)(pinTargets[p].Y - pinRests[p].Y);
                    bx[v0] += pinWeight * b0 * tx; by[v0] += pinWeight * b0 * ty;
                    bx[v1] += pinWeight * b1 * tx; by[v1] += pinWeight * b1 * ty;
                    bx[v2] += pinWeight * b2 * tx; by[v2] += pinWeight * b2 * ty;
                }
                for (var i = 0; i < n; i++)
                {
                    if (isIsolated[i])
                    {
                        //恒等行(対角1)なのでレスト位置をそのまま与える
                        bx[i] = rests[i].X;
                        by[i] = rests[i].Y;
                    }
                    else if (isAnchored[i])
                    {
                        bx[i] += pinWeight * rests[i].X;
                        by[i] += pinWeight * rests[i].Y;
                    }
                }

                cholesky.Solve(bx);
                cholesky.Solve(by);
                Array.Copy(bx, qx, n);
                Array.Copy(by, qy, n);
            }

            for (var i = 0; i < n; i++)
                result[i] = new Vector2((float)qx[i], (float)qy[i]);
        }

        /// <summary>
        /// エッジグラフの連結成分を求め、ピン拘束が1つも掛かっていない成分の頂点を返す。
        /// </summary>
        static bool[] FindPinlessComponentVertices(
            int n,
            int[] neighborStart,
            int[] neighborIndex,
            (int V0, int V1, int V2, double B0, double B1, double B2)[] pinAttachments,
            bool[] isIsolated)
        {
            var component = new int[n];
            Array.Fill(component, -1);
            var componentCount = 0;
            var queue = new Queue<int>();
            for (var i = 0; i < n; i++)
            {
                if (isIsolated[i] || component[i] >= 0)
                    continue;
                component[i] = componentCount;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    var v = queue.Dequeue();
                    for (var k = neighborStart[v]; k < neighborStart[v + 1]; k++)
                    {
                        var w = neighborIndex[k];
                        if (component[w] < 0)
                        {
                            component[w] = componentCount;
                            queue.Enqueue(w);
                        }
                    }
                }
                componentCount++;
            }

            var hasPin = new bool[componentCount];
            foreach (var (v0, v1, v2, _, _, _) in pinAttachments)
            {
                if (component[v0] >= 0) hasPin[component[v0]] = true;
                if (component[v1] >= 0) hasPin[component[v1]] = true;
                if (component[v2] >= 0) hasPin[component[v2]] = true;
            }

            var isAnchored = new bool[n];
            for (var i = 0; i < n; i++)
                isAnchored[i] = component[i] >= 0 && !hasPin[component[i]];
            return isAnchored;
        }

        /// <summary>
        /// forward rigid-MLS（Schaefer 2006）。反復の初期値として使う。
        /// </summary>
        Vector2 ForwardRigidMls(Vector2 v, IReadOnlyList<Vector2> targets)
        {
            var n = pinRests.Length;
            if (n == 1)
                return v + (targets[0] - pinRests[0]);

            const double Epsilon = 1e-6;
            const double Alpha = 2.0;
            var scale = Math.Max(mesh.Width, mesh.Height);
            var scaleInvSq = 1.0 / (scale * scale);

            Span<double> weights = n <= 256 ? stackalloc double[n] : new double[n];
            double totalW = 0;
            double pStarX = 0, pStarY = 0, qStarX = 0, qStarY = 0;
            var minDistSq = double.MaxValue;
            var nearest = 0;

            for (var i = 0; i < n; i++)
            {
                double dx = pinRests[i].X - v.X;
                double dy = pinRests[i].Y - v.Y;
                var distSq = (dx * dx + dy * dy) * scaleInvSq + Epsilon;
                if (distSq < minDistSq) { minDistSq = distSq; nearest = i; }
                var w = Math.Pow(distSq, -Alpha);
                weights[i] = w;
                totalW += w;
                pStarX += w * pinRests[i].X;
                pStarY += w * pinRests[i].Y;
                qStarX += w * targets[i].X;
                qStarY += w * targets[i].Y;
            }

            if (minDistSq < Epsilon * 4 || double.IsInfinity(totalW))
                return targets[nearest];

            var invW = 1.0 / totalW;
            pStarX *= invW; pStarY *= invW;
            qStarX *= invW; qStarY *= invW;

            var vHatX = v.X - pStarX;
            var vHatY = v.Y - pStarY;
            var vHatLen = Math.Sqrt(vHatX * vHatX + vHatY * vHatY);

            double frX = 0, frY = 0;
            for (var i = 0; i < n; i++)
            {
                var pHatX = pinRests[i].X - pStarX;
                var pHatY = pinRests[i].Y - pStarY;
                var qHatX = targets[i].X - qStarX;
                var qHatY = targets[i].Y - qStarY;

                var dotPV = pHatX * vHatX + pHatY * vHatY;
                var dotPperpV = -pHatY * vHatX + pHatX * vHatY;

                var w = weights[i];
                frX += w * (dotPV * qHatX - dotPperpV * qHatY);
                frY += w * (dotPV * qHatY + dotPperpV * qHatX);
            }

            var frLen = Math.Sqrt(frX * frX + frY * frY);
            if (frLen < Epsilon)
                return new Vector2((float)(v.X - pStarX + qStarX), (float)(v.Y - pStarY + qStarY));

            var normScale = vHatLen / frLen;
            return new Vector2((float)(normScale * frX + qStarX), (float)(normScale * frY + qStarY));
        }
    }
}
