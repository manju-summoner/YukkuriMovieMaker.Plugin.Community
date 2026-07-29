using System;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// YMM4のピクセル形式（premultiplied BGRA 8bit・上から下への行順）と
    /// OFXへ供給する形式（RGBA float・下から上への行順）の相互変換。
    /// 入力は premultiplied のまま受け渡すため、アルファの除算・乗算は行わない
    /// （入力クリップの kOfxImageEffectPropPreMultiplication で premultiplied を宣言している）。
    /// 出力はプラグインが GetClipPreferences で宣言したpremultiplication状態に応じて
    /// premultiplied へ揃える（unpremultiplied はアルファを乗算し、opaque はアルファを1へ確定する）。
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

        /// <summary>
        /// RGBA float（下から上への行順）を premultiplied BGRA（上から下への行順）へ変換する。
        /// preMultiplication にはソース画像のpremultiplication状態
        /// （<see cref="OfxConstants.ImagePreMultiplied"/> / <see cref="OfxConstants.ImageUnPreMultiplied"/> /
        /// <see cref="OfxConstants.ImageOpaque"/>）を渡す
        /// </summary>
        public static void RgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height, string preMultiplication)
        {
            switch (preMultiplication)
            {
                case OfxConstants.ImageUnPreMultiplied:
                    UnPreMultipliedRgbaBottomUpToBgraTopDown(source, destination, width, height);
                    break;
                case OfxConstants.ImageOpaque:
                    OpaqueRgbaBottomUpToBgraTopDown(source, destination, width, height);
                    break;
                default:
                    RgbaBottomUpToBgraTopDown(source, destination, width, height);
                    break;
            }
        }

        static void RgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
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

        /// <summary>unpremultiplied宣言の出力用: RGBへアルファを乗算してpremultiplied BGRAへ揃える</summary>
        static void UnPreMultipliedRgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
        {
            fixed (byte* destinationPointer = destination)
            {
                for (var y = 0; y < height; y++)
                {
                    var sourceRow = source + (long)(height - 1 - y) * width * 4;
                    var destinationRow = destinationPointer + (long)y * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        var alpha = sourceRow[3];
                        destinationRow[0] = ToByte(sourceRow[2] * alpha);    // B
                        destinationRow[1] = ToByte(sourceRow[1] * alpha);    // G
                        destinationRow[2] = ToByte(sourceRow[0] * alpha);    // R
                        destinationRow[3] = ToByte(alpha);                   // A
                        sourceRow += 4;
                        destinationRow += 4;
                    }
                }
            }
        }

        /// <summary>opaque宣言の出力用: アルファを1（不透明）へ確定する（プラグインのアルファ値は不定として読まない）</summary>
        static void OpaqueRgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
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
                        destinationRow[3] = 255;                     // A
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
