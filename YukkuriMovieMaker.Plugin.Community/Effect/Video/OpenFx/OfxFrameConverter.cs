using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

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
        //Parallel.For自体の起動コストが変換時間を上回る小画像は単スレッドで処理する。
        //閾値はOpenFxCpuPerformanceTest（ReleaseLite・16論理コア）の実測に基づく。
        //2304〜9216画素は未計測。9216画素が全条件（両方向・busy有無）で並列有利を
        //確認できた最小の実測サイズなので、これを境界とする。
        //busy計測は論理コア1/4のbusyスレッド併走時の値であり、プレビュー＋出力同時などのレンダースレッド多重時は未検証。
        const int ParallelPixelThreshold = 9216;

        /// <summary>AVX2非対応環境と同じスカラー経路を回帰テストで強制するためのフック。</summary>
        internal static volatile bool ForceScalarForTesting;

        /// <summary>行並列化の閾値を性能計測と回帰テストで上書きするためのフック。0は上書きなし。</summary>
        internal static volatile int ParallelPixelThresholdOverrideForTesting;

        /// <summary>行並列化の最大並列度を回帰テストで上書きするためのフック。0は上書きなし。</summary>
        internal static volatile int MaxDegreeOfParallelismOverrideForTesting;

        //経路記録は呼び出しスレッド上でのみ書き、テストも同一スレッドで読む前提
        //（OpenFXに触れるテストfixtureは [NonParallelizable] 必須。並列fixtureから読むと非決定的になる）
        internal static bool LastRunWasParallelForTesting;

        internal static bool LastUsedAvx2ForTesting;

        static bool UseAvx2 => Avx2.IsSupported && !ForceScalarForTesting;

        static int MaxDegreeOfParallelism
        {
            get
            {
                var overrideValue = MaxDegreeOfParallelismOverrideForTesting;
                return overrideValue != 0 ? overrideValue : Math.Max(1, Environment.ProcessorCount * 3 / 4);
            }
        }

        public static void BgraTopDownToRgbaBottomUp(ReadOnlySpan<byte> source, float* destination, int width, int height)
        {
            var useAvx2 = UseAvx2;
            LastUsedAvx2ForTesting = useAvx2;
            fixed (byte* sourcePointer = source)
            {
                RunRows(
                    (nint)sourcePointer,
                    (nint)destination,
                    width,
                    height,
                    useAvx2 ? BgraTopDownToRgbaBottomUpAvx2 : BgraTopDownToRgbaBottomUpScalar);
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
            var useAvx2 = UseAvx2;
            LastUsedAvx2ForTesting = useAvx2;
            fixed (byte* destinationPointer = destination)
            {
                RunRows(
                    (nint)source,
                    (nint)destinationPointer,
                    width,
                    height,
                    useAvx2 ? PremultipliedRgbaBottomUpToBgraTopDownAvx2 : PremultipliedRgbaBottomUpToBgraTopDownScalar);
            }
        }

        /// <summary>unpremultiplied宣言の出力用: RGBへアルファを乗算してpremultiplied BGRAへ揃える</summary>
        static void UnPreMultipliedRgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
        {
            var useAvx2 = UseAvx2;
            LastUsedAvx2ForTesting = useAvx2;
            fixed (byte* destinationPointer = destination)
            {
                RunRows(
                    (nint)source,
                    (nint)destinationPointer,
                    width,
                    height,
                    useAvx2 ? UnPremultipliedRgbaBottomUpToBgraTopDownAvx2 : UnPremultipliedRgbaBottomUpToBgraTopDownScalar);
            }
        }

        /// <summary>opaque宣言の出力用: アルファを1（不透明）へ確定する（プラグインのアルファ値は不定として読まない）</summary>
        static void OpaqueRgbaBottomUpToBgraTopDown(float* source, Span<byte> destination, int width, int height)
        {
            var useAvx2 = UseAvx2;
            LastUsedAvx2ForTesting = useAvx2;
            fixed (byte* destinationPointer = destination)
            {
                RunRows(
                    (nint)source,
                    (nint)destinationPointer,
                    width,
                    height,
                    useAvx2 ? OpaqueRgbaBottomUpToBgraTopDownAvx2 : OpaqueRgbaBottomUpToBgraTopDownScalar);
            }
        }

        delegate void ConvertRows(nint source, nint destination, int width, int height, int y0, int y1);

        static void RunRows(nint source, nint destination, int width, int height, ConvertRows convert)
        {
            var maxDegreeOfParallelism = MaxDegreeOfParallelism;
            var thresholdOverride = ParallelPixelThresholdOverrideForTesting;
            var parallelPixelThreshold = thresholdOverride != 0 ? thresholdOverride : ParallelPixelThreshold;
            //高さ1の横長画像は画素数が閾値を超えても分割できず、Parallel.Forの起動コストだけが乗る
            var bands = Math.Min(height, maxDegreeOfParallelism);
            if ((long)width * height < parallelPixelThreshold || bands <= 1)
            {
                LastRunWasParallelForTesting = false;
                convert(source, destination, width, height, 0, height);
                return;
            }

            LastRunWasParallelForTesting = true;
            Parallel.For(
                0,
                bands,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                band =>
                {
                    var y0 = (int)((long)height * band / bands);
                    var y1 = (int)((long)height * (band + 1) / bands);
                    convert(source, destination, width, height, y0, y1);
                });
        }

        static void BgraTopDownToRgbaBottomUpScalar(nint sourceAddress, nint destinationAddress, int width, int height, int y0, int y1)
        {
            const float scale = 1f / 255f;
            var source = (byte*)sourceAddress;
            var destination = (float*)destinationAddress;
            for (var y = y0; y < y1; y++)
            {
                var sourceRow = source + (long)y * width * 4;
                var destinationRow = destination + (long)(height - 1 - y) * width * 4;
                for (var x = 0; x < width; x++)
                {
                    destinationRow[0] = sourceRow[2] * scale;
                    destinationRow[1] = sourceRow[1] * scale;
                    destinationRow[2] = sourceRow[0] * scale;
                    destinationRow[3] = sourceRow[3] * scale;
                    sourceRow += 4;
                    destinationRow += 4;
                }
            }
        }

        static void BgraTopDownToRgbaBottomUpAvx2(nint sourceAddress, nint destinationAddress, int width, int height, int y0, int y1)
        {
            var source = (byte*)sourceAddress;
            var destination = (float*)destinationAddress;
            var scale = Vector256.Create(1f / 255f);
            var shuffle128 = Vector128.Create(
                (byte)2, 1, 0, 3, 6, 5, 4, 7,
                10, 9, 8, 11, 14, 13, 12, 15);
            var shuffle = Vector256.Create(shuffle128, shuffle128);

            for (var y = y0; y < y1; y++)
            {
                var sourceRow = source + (long)y * width * 4;
                var destinationRow = destination + (long)(height - 1 - y) * width * 4;
                var x = 0;
                for (; x + 8 <= width; x += 8)
                {
                    var rgba = Avx2.Shuffle(Vector256.Load(sourceRow), shuffle);
                    var lower = rgba.GetLower();
                    var upper = rgba.GetUpper();
                    Vector256.Store(Avx.Multiply(Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(lower)), scale), destinationRow);
                    Vector256.Store(Avx.Multiply(Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(lower, 8))), scale), destinationRow + 8);
                    Vector256.Store(Avx.Multiply(Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(upper)), scale), destinationRow + 16);
                    Vector256.Store(Avx.Multiply(Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(upper, 8))), scale), destinationRow + 24);
                    sourceRow += 32;
                    destinationRow += 32;
                }
                ConvertBgraTail(sourceRow, destinationRow, width - x);
            }
        }

        static void ConvertBgraTail(byte* source, float* destination, int count)
        {
            const float scale = 1f / 255f;
            for (var x = 0; x < count; x++)
            {
                destination[0] = source[2] * scale;
                destination[1] = source[1] * scale;
                destination[2] = source[0] * scale;
                destination[3] = source[3] * scale;
                source += 4;
                destination += 4;
            }
        }

        static void PremultipliedRgbaBottomUpToBgraTopDownScalar(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownScalar(source, destination, width, height, y0, y1, 0);

        static void UnPremultipliedRgbaBottomUpToBgraTopDownScalar(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownScalar(source, destination, width, height, y0, y1, 1);

        static void OpaqueRgbaBottomUpToBgraTopDownScalar(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownScalar(source, destination, width, height, y0, y1, 2);

        static void RgbaBottomUpToBgraTopDownScalar(nint sourceAddress, nint destinationAddress, int width, int height, int y0, int y1, int mode)
        {
            var source = (float*)sourceAddress;
            var destination = (byte*)destinationAddress;
            var unPremultiplied = mode == 1;
            var opaque = mode == 2;
            for (var y = y0; y < y1; y++)
            {
                var sourceRow = source + (long)(height - 1 - y) * width * 4;
                var destinationRow = destination + (long)y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var alpha = sourceRow[3];
                    var multiplier = unPremultiplied ? alpha : 1f;
                    destinationRow[0] = ToByte(sourceRow[2] * multiplier);
                    destinationRow[1] = ToByte(sourceRow[1] * multiplier);
                    destinationRow[2] = ToByte(sourceRow[0] * multiplier);
                    destinationRow[3] = opaque ? (byte)255 : ToByte(alpha);
                    sourceRow += 4;
                    destinationRow += 4;
                }
            }
        }

        static void PremultipliedRgbaBottomUpToBgraTopDownAvx2(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownAvx2(source, destination, width, height, y0, y1, 0);

        static void UnPremultipliedRgbaBottomUpToBgraTopDownAvx2(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownAvx2(source, destination, width, height, y0, y1, 1);

        static void OpaqueRgbaBottomUpToBgraTopDownAvx2(nint source, nint destination, int width, int height, int y0, int y1)
            => RgbaBottomUpToBgraTopDownAvx2(source, destination, width, height, y0, y1, 2);

        static void RgbaBottomUpToBgraTopDownAvx2(nint sourceAddress, nint destinationAddress, int width, int height, int y0, int y1, int mode)
        {
            var source = (float*)sourceAddress;
            var destination = (byte*)destinationAddress;
            //0〜1のfloat値を8bit値へ変換するスケール係数。
            var scale = Vector256.Create(255f);
            var half = Vector256.Create(0.5f);
            var zero = Vector256<float>.Zero;
            //丸め後の値をbyte範囲へ収めるクランプ上限。
            var clampUpper = Vector256.Create(255f);
            var alphaIndices = Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7);
            var one = Vector256.Create(1f);
            for (var y = y0; y < y1; y++)
            {
                var sourceRow = source + (long)(height - 1 - y) * width * 4;
                var destinationRow = destination + (long)y * width * 4;
                var x = 0;
                for (; x + 8 <= width; x += 8)
                {
                    var lower16 = Vector256.Narrow(
                        ToByteInt32(PrepareOutputVector(Vector256.Load(sourceRow), mode, alphaIndices, one), scale, half, zero, clampUpper),
                        ToByteInt32(PrepareOutputVector(Vector256.Load(sourceRow + 8), mode, alphaIndices, one), scale, half, zero, clampUpper));
                    var upper16 = Vector256.Narrow(
                        ToByteInt32(PrepareOutputVector(Vector256.Load(sourceRow + 16), mode, alphaIndices, one), scale, half, zero, clampUpper),
                        ToByteInt32(PrepareOutputVector(Vector256.Load(sourceRow + 24), mode, alphaIndices, one), scale, half, zero, clampUpper));
                    Vector256.Store(Vector256.Narrow(lower16.AsUInt16(), upper16.AsUInt16()), destinationRow);
                    sourceRow += 32;
                    destinationRow += 32;
                }
                ConvertRgbaTail(sourceRow, destinationRow, width - x, mode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<float> PrepareOutputVector(
            Vector256<float> value,
            int mode,
            Vector256<int> alphaIndices,
            Vector256<float> one)
        {
            const byte alphaLanes = 0b10001000;
            const byte bgraSwizzle = (3 << 6) | (0 << 4) | (1 << 2) | 2;
            if (mode == 1)
            {
                var multiplied = Avx.Multiply(value, Avx2.PermuteVar8x32(value, alphaIndices));
                value = Avx.Blend(multiplied, value, alphaLanes);
            }
            else if (mode == 2)
            {
                value = Avx.Blend(value, one, alphaLanes);
            }
            return Avx.Shuffle(value, value, bgraSwizzle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<int> ToByteInt32(
            Vector256<float> value,
            Vector256<float> scale,
            Vector256<float> half,
            Vector256<float> zero,
            Vector256<float> clampUpper)
        {
            var scaled = Avx.Add(Avx.Multiply(value, scale), half);
            //MAXPSはNaN時に第2オペランドを返すため、scaledを第1、zeroを第2に固定する。
            //この順序なら現行ToByteの(byte)NaNと同じ0になり、以後のMINPSへNaNを残さない。
            var clamped = Avx.Min(Avx.Max(scaled, zero), clampUpper);
            return Avx.ConvertToVector256Int32WithTruncation(clamped);
        }

        static void ConvertRgbaTail(float* source, byte* destination, int count, int mode)
        {
            var unPremultiplied = mode == 1;
            var opaque = mode == 2;
            for (var x = 0; x < count; x++)
            {
                var alpha = source[3];
                var multiplier = unPremultiplied ? alpha : 1f;
                destination[0] = ToByte(source[2] * multiplier);
                destination[1] = ToByte(source[1] * multiplier);
                destination[2] = ToByte(source[0] * multiplier);
                destination[3] = opaque ? (byte)255 : ToByte(alpha);
                source += 4;
                destination += 4;
            }
        }

        static byte ToByte(float value)
        {
            var scaled = value * 255f + 0.5f;
            return scaled <= 0 ? (byte)0 : scaled >= 255 ? (byte)255 : (byte)scaled;
        }
    }
}
