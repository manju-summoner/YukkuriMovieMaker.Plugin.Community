using System;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// 対称正定値バンド行列のCholesky分解と前進/後退代入。
    /// グリッドメッシュのラプラシアンは自然順序で帯幅が小さいため、
    /// 汎用疎行列ソルバーなしで毎フレームの求解を高速に行える。
    /// </summary>
    internal sealed class BandCholesky
    {
        readonly int n;
        readonly int bandwidth;
        readonly int stride;
        //下三角バンド格納: 行iの列j(i-bandwidth ≦ j ≦ i)を data[i * stride + (j - i + bandwidth)] に置く
        readonly double[] data;

        public int Size => n;
        public int Bandwidth => bandwidth;

        public BandCholesky(int size, int bandwidth)
        {
            n = size;
            this.bandwidth = bandwidth;
            stride = bandwidth + 1;
            data = new double[(long)n * stride < int.MaxValue ? n * stride : throw new ArgumentOutOfRangeException(nameof(size))];
        }

        public void Clear() => Array.Clear(data);

        /// <summary>A[i,j] (j ≦ i) に値を加算する。分解前の組み立てに使う。</summary>
        public void Add(int i, int j, double value)
        {
            if (j > i)
                (i, j) = (j, i);
            var offset = j - i + bandwidth;
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(j), "band外の要素は格納できません");
            data[i * stride + offset] = data[i * stride + offset] + value;
        }

        /// <summary>
        /// 組み立て済みの行列をin-placeでCholesky分解する。
        /// 正定値でない場合はfalseを返す。
        /// </summary>
        public bool Factorize()
        {
            for (var i = 0; i < n; i++)
            {
                var jMin = Math.Max(0, i - bandwidth);
                for (var j = jMin; j <= i; j++)
                {
                    var sum = data[i * stride + (j - i + bandwidth)];
                    var kMin = Math.Max(jMin, j - bandwidth);
                    for (var k = kMin; k < j; k++)
                        sum -= data[i * stride + (k - i + bandwidth)] * data[j * stride + (k - j + bandwidth)];

                    if (i == j)
                    {
                        if (sum <= 0 || double.IsNaN(sum))
                            return false;
                        data[i * stride + bandwidth] = Math.Sqrt(sum);
                    }
                    else
                    {
                        data[i * stride + (j - i + bandwidth)] = sum / data[j * stride + bandwidth];
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// L L^T x = b を解く。bはin-placeで解に置き換わる。
        /// </summary>
        public void Solve(Span<double> b)
        {
            //前進代入 L y = b
            for (var i = 0; i < n; i++)
            {
                var sum = b[i];
                var jMin = Math.Max(0, i - bandwidth);
                for (var j = jMin; j < i; j++)
                    sum -= data[i * stride + (j - i + bandwidth)] * b[j];
                b[i] = sum / data[i * stride + bandwidth];
            }

            //後退代入 L^T x = y
            for (var i = n - 1; i >= 0; i--)
            {
                var sum = b[i];
                var jMax = Math.Min(n - 1, i + bandwidth);
                for (var j = i + 1; j <= jMax; j++)
                    sum -= data[j * stride + (i - j + bandwidth)] * b[j];
                b[i] = sum / data[i * stride + bandwidth];
            }
        }
    }
}
