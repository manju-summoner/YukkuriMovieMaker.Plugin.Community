using System;
using System.Collections.Generic;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// ボーン階層のワールド変換を計算する純粋関数群。
    /// 各ボーンはレスト空間上のジョイント位置を中心に回転し、親の変換が子に合成される。
    /// </summary>
    internal static class PuppetBoneEvaluator
    {
        public readonly record struct BoneSample(Guid Id, Guid ParentId, Vector2 Joint, float AngleRadians);

        /// <summary>
        /// 各ボーンのワールド変換を計算する（入力と同じインデックス順）。
        /// 親が見つからない場合はルート扱い。循環参照は逆辺を無視して打ち切る。
        /// </summary>
        public static Matrix3x2[] ComputeWorldTransforms(IReadOnlyList<BoneSample> bones)
        {
            var count = bones.Count;
            var result = new Matrix3x2[count];
            var indexById = new Dictionary<Guid, int>(count);
            for (var i = 0; i < count; i++)
                indexById.TryAdd(bones[i].Id, i);

            //0=未計算 1=計算中 2=計算済み
            var states = new byte[count];
            for (var i = 0; i < count; i++)
                Compute(i);
            return result;

            Matrix3x2 Compute(int index)
            {
                if (states[index] == 2)
                    return result[index];
                //計算中のボーンを親として参照する＝循環。逆辺を無視する（親なし扱い）
                if (states[index] == 1)
                    return Matrix3x2.Identity;

                states[index] = 1;
                var bone = bones[index];
                var world = Matrix3x2.CreateRotation(bone.AngleRadians, bone.Joint);
                if (bone.ParentId != Guid.Empty
                    && bone.ParentId != bone.Id
                    && indexById.TryGetValue(bone.ParentId, out var parentIndex)
                    && parentIndex != index)
                {
                    //行ベクトル規約(v * M)のため、ローカル変換を先に適用してから親を掛ける
                    world *= Compute(parentIndex);
                }
                result[index] = world;
                states[index] = 2;
                return world;
            }
        }

        /// <summary>
        /// 揺れの角度(ラジアン)を計算する。閉形式・決定論的で、シークしても同じ結果を返す。
        /// </summary>
        public static float GetSwayAngleRadians(double amplitudeDeg, double periodSec, double phaseDeg, double timeSec)
        {
            if (periodSec <= 0 || amplitudeDeg == 0)
                return 0f;
            var phase = 2 * Math.PI * (timeSec / periodSec) + phaseDeg * Math.PI / 180;
            return (float)(amplitudeDeg * Math.PI / 180 * Math.Sin(phase));
        }

        public readonly record struct SwaySample(Guid Id, Guid ParentId, double AmplitudeDeg, double PeriodSec, double PhaseDeg, double FlexibilityDeg, double PropagationPercent);

        /// <summary>
        /// 各ボーンの揺れ角度(ラジアン)を計算する（入力と同じインデックス順）。
        /// ボーンの揺れは1世代ごとに、位相がしなり分だけ遅れ、振幅が伝播率倍されて子孫へ伝わる。
        /// 伝播0%で子へは伝わらず、100%でそのまま、100%超で先端ほど増幅される。
        /// </summary>
        public static float[] ComputeSwayAngles(IReadOnlyList<SwaySample> bones, double timeSec)
        {
            var count = bones.Count;
            var result = new float[count];
            var indexById = new Dictionary<Guid, int>(count);
            for (var i = 0; i < count; i++)
                indexById.TryAdd(bones[i].Id, i);

            var visited = new bool[count];
            for (var i = 0; i < count; i++)
            {
                Array.Clear(visited);
                var total = 0f;
                var current = i;
                //自身(距離0)から祖先へ遡り、各ボーンの揺れを位相遅れ・減衰付きで集計する
                for (var distance = 0; distance < count; distance++)
                {
                    if (visited[current])
                        break;
                    visited[current] = true;

                    var bone = bones[current];
                    if (bone.AmplitudeDeg != 0)
                    {
                        var propagationRate = Math.Max(0, bone.PropagationPercent / 100);
                        var amplitude = bone.AmplitudeDeg * Math.Pow(propagationRate, distance);
                        if (amplitude != 0)
                            total += GetSwayAngleRadians(amplitude, bone.PeriodSec, bone.PhaseDeg - distance * bone.FlexibilityDeg, timeSec);
                    }

                    if (bone.ParentId == Guid.Empty
                        || bone.ParentId == bone.Id
                        || !indexById.TryGetValue(bone.ParentId, out var parent)
                        || parent == current)
                        break;
                    current = parent;
                }
                result[i] = total;
            }
            return result;
        }
    }
}
