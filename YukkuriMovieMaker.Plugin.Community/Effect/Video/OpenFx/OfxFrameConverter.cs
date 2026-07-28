using System;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// YMM4のピクセル形式（premultiplied BGRA 8bit・上から下への行順）と
    /// OFXへ供給する形式（premultiplied RGBA float・下から上への行順）の相互変換。
    /// premultiplied のまま受け渡すため、アルファの除算・乗算は行わない
    /// （クリップの kOfxImageEffectPropPreMultiplication で premultiplied を宣言している）。
    /// </summary>
    internal static unsafe class OfxFrameConverter
    {
        public static void BgraTopDownToRgbaBottomUp(ReadOnlySpan<byte> source, float* destination, int width, int height)
        {
            const float scale = 1f / 255f;
            fixed (byte* sourcePointer = source)
            {
                for (var y = 0; y < height; y++)
                {
                    var sourceRow = sourcePointer + (long)y * width * 4;
                    var destinationRow = destination + (long)(height - 1 - y) * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        destinationRow[0] = sourceRow[2] * scale;    // R
                        destinationRow[1] = sourceRow[1] * scale;    // G
                        destinationRow[2] = sourceRow[0] * scale;    // B
                        destinationRow[3] = sourceRow[3] * scale;    // A
                        sourceRow += 4;
                        destinationRow += 4;
                    }
                }
            }
        }

        public static void RgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
        {
            fixed (byte* destinationPointer = destination)
            {
                for (var y = 0; y < height; y++)
                {
                    var sourceRow = source + (long)(height - 1 - y) * width * 4;
                    var destinationRow = destinationPointer + (long)y * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        destinationRow[0] = ToByte(sourceRow[2]);    // B
                        destinationRow[1] = ToByte(sourceRow[1]);    // G
                        destinationRow[2] = ToByte(sourceRow[0]);    // R
                        destinationRow[3] = ToByte(sourceRow[3]);    // A
                        sourceRow += 4;
                        destinationRow += 4;
                    }
                }
            }
        }

        static byte ToByte(float value)
        {
            var scaled = value * 255f + 0.5f;
            return scaled <= 0 ? (byte)0 : scaled >= 255 ? (byte)255 : (byte)scaled;
        }
    }
}
