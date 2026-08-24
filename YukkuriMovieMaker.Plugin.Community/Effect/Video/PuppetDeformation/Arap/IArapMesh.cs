using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// ARAP変形が要求するメッシュ情報。
    /// 座標系は画像中央原点のローカル座標（ピンと同じ空間）。
    /// グリッドメッシュ（ArapGridMesh）と輪郭メッシュ（ArapContourMesh）の共通インターフェース。
    /// </summary>
    internal interface IArapMesh
    {
        float Width { get; }
        float Height { get; }

        /// <summary>レスト状態の頂点位置（ローカル座標）</summary>
        Vector2[] RestPositions { get; }

        /// <summary>三角形リスト（3頂点インデックスずつ）</summary>
        int[] TriangleIndices { get; }

        /// <summary>重複なしのエッジと対応するcotan重み</summary>
        (int A, int B, double Weight)[] Edges { get; }

        int VertexCount { get; }

        /// <summary>
        /// BandCholeskyに渡すバンド幅。頂点番号付けに依存し、
        /// 全エッジ・全三角形内頂点ペアのインデックス差がこの値以下であること。
        /// </summary>
        int SolverBandwidth { get; }

        /// <summary>
        /// ローカル座標の点が属する三角形と重心座標を求める。範囲外はメッシュ内にクランプする。
        /// メッシュ外の点は最寄りの三角形へアタッチする。
        /// </summary>
        (int V0, int V1, int V2, double B0, double B1, double B2) FindContainingTriangle(Vector2 p);
    }
}
