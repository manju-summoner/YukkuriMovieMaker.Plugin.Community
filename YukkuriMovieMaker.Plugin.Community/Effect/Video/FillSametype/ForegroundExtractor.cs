using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype
{
    internal static unsafe class ForegroundExtractor
    {
        public static void Extract(byte* source, int pitch, int width, int height, int[] foreground)
        {
            fixed (int* destination = foreground)
            {
                if (Avx2.IsSupported)
                {
                    for (int y = 0; y < height; y++)
                        ExtractRowAvx2(source + (nint)y * pitch, width, destination + (nint)y * width);
                }
                else if (Sse2.IsSupported)
                {
                    for (int y = 0; y < height; y++)
                        ExtractRowSse2(source + (nint)y * pitch, width, destination + (nint)y * width);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                        ExtractRowScalar(source + (nint)y * pitch, width, destination + (nint)y * width, 0);
                }
            }
        }

        static void ExtractRowAvx2(byte* row, int width, int* dst)
        {
            int x = 0;
            var one = Vector256<int>.One;

            for (; x <= width - 8; x += 8)
            {
                var bytes = Avx.LoadVector256(row + (nint)x * 4);
                var alpha = Avx2.ShiftRightLogical(bytes.AsUInt32(), 24).AsInt32();
                var mask = Avx2.CompareEqual(alpha, Vector256<int>.Zero);
                var result = Avx2.AndNot(mask, one);
                Avx.Store(dst + x, result);
            }

            ExtractRowScalar(row, width, dst, x);
        }

        static void ExtractRowSse2(byte* row, int width, int* dst)
        {
            int x = 0;
            var one = Vector128<int>.One;

            for (; x <= width - 4; x += 4)
            {
                var bytes = Sse2.LoadVector128(row + (nint)x * 4);
                var alpha = Sse2.ShiftRightLogical(bytes.AsUInt32(), 24).AsInt32();
                var mask = Sse2.CompareEqual(alpha, Vector128<int>.Zero);
                var result = Sse2.AndNot(mask, one);
                Sse2.Store(dst + x, result);
            }

            ExtractRowScalar(row, width, dst, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ExtractRowScalar(byte* row, int width, int* dst, int start)
        {
            for (int x = start; x < width; x++)
                dst[x] = row[x * 4 + 3] != 0 ? 1 : 0;
        }
    }
}
