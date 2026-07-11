namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    /// <summary>
    /// パーティクル出力エフェクトの静的頂点バッファ（粒子スロット1個 = 三角形2枚 = 6頂点）を生成する。
    /// 各頂点は float4 { スロット番号, 予約, コーナーオフセットX, コーナーオフセットY }（コーナーは-1〜+1の正規化値）。
    /// 粒子の発生位置・大きさ・運動はすべて頂点シェーダーが定数から計算するため、バッファはスロット数が変わらない限り不変。
    /// </summary>
    internal static class ParticleOutputVertexBufferBuilder
    {
        public static byte[] Build(int slotCount)
        {
            slotCount = Math.Clamp(slotCount, 1, ParticleOutputCustomEffect.MaxParticles);

            var floats = new float[slotCount * 6 * 4];
            var fi = 0;
            for (var slot = 0; slot < slotCount; slot++)
            {
                //三角形 (左上,右上,右下) と (左上,右下,左下)
                Write(floats, ref fi, slot, -1, -1);
                Write(floats, ref fi, slot, +1, -1);
                Write(floats, ref fi, slot, +1, +1);
                Write(floats, ref fi, slot, -1, -1);
                Write(floats, ref fi, slot, +1, +1);
                Write(floats, ref fi, slot, -1, +1);
            }

            var bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static void Write(float[] buffer, ref int i, float slot, float cornerX, float cornerY)
        {
            buffer[i++] = slot;
            buffer[i++] = 0;
            buffer[i++] = cornerX;
            buffer[i++] = cornerY;
        }
    }
}
