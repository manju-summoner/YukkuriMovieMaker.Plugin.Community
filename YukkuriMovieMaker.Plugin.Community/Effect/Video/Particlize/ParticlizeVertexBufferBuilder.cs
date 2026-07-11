using Vortice;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    /// <summary>
    /// 粒子化エフェクトの静的頂点バッファ（粒子1個 = 三角形2枚 = 6頂点）を生成する。
    /// 各頂点は float4 { 粒子中心X, 粒子中心Y, コーナーオフセットX, コーナーオフセットY }（シーン座標px）。
    /// 粒子の運動は頂点シェーダーが定数から計算するため、バッファは入力範囲・粒子サイズが変わらない限り不変。
    /// </summary>
    internal static class ParticlizeVertexBufferBuilder
    {
        /// <summary>
        /// 入力範囲を粒子サイズで格子分割した頂点データを生成する。
        /// セル数が上限を超える場合は実効粒子サイズを引き上げ、常に画像全体を均一に覆う。
        /// </summary>
        public static byte[] Build(RawRectF bounds, double size)
        {
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (!(width > 0) || !(height > 0))
                return new byte[6 * ParticlizeCustomEffect.VertexStride];//面積0の粒子1個（何も描画しない）

            size = Math.Max(1, size);
            var effectiveSize = Math.Max(size, Math.Max(width, height) / (double)ParticlizeCustomEffect.MaxCellsPerAxis);
            var countX = Math.Clamp((int)Math.Ceiling(width / effectiveSize), 1, ParticlizeCustomEffect.MaxCellsPerAxis);
            var countY = Math.Clamp((int)Math.Ceiling(height / effectiveSize), 1, ParticlizeCustomEffect.MaxCellsPerAxis);
            var spacingX = width / countX;
            var spacingY = height / countY;
            var halfX = spacingX * 0.5f;
            var halfY = spacingY * 0.5f;

            var floats = new float[countX * countY * 6 * 4];
            var fi = 0;
            for (var y = 0; y < countY; y++)
            {
                var centerY = bounds.Top + (y + 0.5f) * spacingY;
                for (var x = 0; x < countX; x++)
                {
                    var centerX = bounds.Left + (x + 0.5f) * spacingX;
                    //三角形 (左上,右上,右下) と (左上,右下,左下)
                    Write(floats, ref fi, centerX, centerY, -halfX, -halfY);
                    Write(floats, ref fi, centerX, centerY, +halfX, -halfY);
                    Write(floats, ref fi, centerX, centerY, +halfX, +halfY);
                    Write(floats, ref fi, centerX, centerY, -halfX, -halfY);
                    Write(floats, ref fi, centerX, centerY, +halfX, +halfY);
                    Write(floats, ref fi, centerX, centerY, -halfX, +halfY);
                }
            }

            var bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static void Write(float[] buffer, ref int i, float centerX, float centerY, float cornerX, float cornerY)
        {
            buffer[i++] = centerX;
            buffer[i++] = centerY;
            buffer[i++] = cornerX;
            buffer[i++] = cornerY;
        }
    }
}
