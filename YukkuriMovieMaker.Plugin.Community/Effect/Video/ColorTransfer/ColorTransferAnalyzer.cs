using System.Buffers.Binary;
using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    internal sealed class ColorTransferAnalyzer
    {
        public const int LutSize = 128;
        public const int LutByteSize = LutSize * 16;
        public const int GridSize = 16;
        public const int LocalDeltaByteSize = GridSize * GridSize * 16;

        private const int Channels = 3;
        private const int Bins = 256;
        private const int SmoothingPasses = 4;
        private const int SrgbTableSize = 1024;
        private const int MinimumAlpha = 8;
        private const double MinimumSpan = 1e-3;
        private const double MinimumDeviation = 1e-5;
        private const double DomainMargin = 0.1;

        private static readonly float[] SrgbToLinearTable = CreateSrgbToLinearTable();
        private static readonly float[] OpaqueLinearTable = CreateOpaqueLinearTable();
        private static readonly double[] BinMinimum = [0.0, -0.35, -0.35];
        private static readonly double[] BinMaximum = [1.0, 0.35, 0.35];

        private readonly Accumulator _source = new();
        private readonly Accumulator _reference = new();
        private readonly double[] _curve = new double[LutSize];
        private readonly double[] _slope = new double[LutSize - 1];
        private readonly float[] _values = new float[Channels * LutSize];
        private readonly byte[] _lutBytes = new byte[LutByteSize];
        private readonly double[] _domainMinimum = new double[Channels];
        private readonly double[] _domainSpan = new double[Channels];
        private readonly byte[] _localDeltaBytes = new byte[LocalDeltaByteSize];

        public Vector3 DomainMinimum { get; private set; }

        public Vector3 DomainScale { get; private set; } = Vector3.One;

        public byte[] LutBytes => _lutBytes;

        public byte[] LocalDeltaBytes => _localDeltaBytes;

        public bool Analyze(
            ReadOnlySpan<int> sourcePixels,
            ReadOnlySpan<int> referencePixels,
            int referenceWidth,
            int referenceHeight,
            ColorTransferMode mode,
            double maximumGain)
        {
            var useHistogram = mode == ColorTransferMode.Histogram;

            _source.Reset();
            _reference.Reset();
            _source.Accumulate(sourcePixels, 0, 0, useHistogram);
            _reference.Accumulate(referencePixels, referenceWidth, referenceHeight, useHistogram);

            if (_source.Weight <= 0.0 || _reference.Weight <= 0.0)
                return false;

            _source.Resolve(useHistogram);
            _reference.Resolve(useHistogram);

            var gain = Math.Max(maximumGain, 1.0);

            for (var channel = 0; channel < Channels; channel++)
            {
                var span = Math.Max(_source.Maximum[channel] - _source.Minimum[channel], MinimumSpan);
                var margin = span * DomainMargin;
                _domainMinimum[channel] = _source.Minimum[channel] - margin;
                _domainSpan[channel] = span + margin * 2.0;
            }

            DomainMinimum = new Vector3(
                (float)_domainMinimum[0],
                (float)_domainMinimum[1],
                (float)_domainMinimum[2]);
            DomainScale = new Vector3(
                (float)(1.0 / _domainSpan[0]),
                (float)(1.0 / _domainSpan[1]),
                (float)(1.0 / _domainSpan[2]));

            for (var channel = 0; channel < Channels; channel++)
                BuildCurve(channel, mode, gain);

            PackLut();
            PackLocalDelta();
            return true;
        }

        private void BuildCurve(int channel, ColorTransferMode mode, double maximumGain)
        {
            var low = _domainMinimum[channel];
            var step = _domainSpan[channel] / (LutSize - 1);
            var sourceMean = _source.Mean[channel];
            var referenceMean = _reference.Mean[channel];

            switch (mode)
            {
                case ColorTransferMode.Mean:
                    {
                        var offset = referenceMean - sourceMean;
                        for (var i = 0; i < LutSize; i++)
                            _curve[i] = low + step * i + offset;
                        break;
                    }
                case ColorTransferMode.MeanAndVariance:
                    {
                        var deviation = Math.Max(_source.Deviation[channel], MinimumDeviation);
                        var scale = Math.Clamp(_reference.Deviation[channel] / deviation, 1.0 / maximumGain, maximumGain);
                        for (var i = 0; i < LutSize; i++)
                            _curve[i] = (low + step * i - sourceMean) * scale + referenceMean;
                        break;
                    }
                default:
                    {
                        for (var i = 0; i < LutSize; i++)
                            _curve[i] = _reference.Quantile(channel, _source.Cumulative(channel, low + step * i));
                        LimitSlope(low, step, sourceMean, maximumGain);
                        break;
                    }
            }

            var target = channel * LutSize;
            for (var i = 0; i < LutSize; i++)
                _values[target + i] = (float)_curve[i];
        }

        private void LimitSlope(double low, double step, double sourceMean, double maximumGain)
        {
            var minimumSlope = 1.0 / maximumGain;
            for (var i = 0; i < LutSize - 1; i++)
                _slope[i] = Math.Clamp((_curve[i + 1] - _curve[i]) / step, minimumSlope, maximumGain);

            var anchor = Math.Clamp((int)Math.Round((sourceMean - low) / step), 0, LutSize - 1);

            for (var i = anchor; i < LutSize - 1; i++)
                _curve[i + 1] = _curve[i] + _slope[i] * step;
            for (var i = anchor; i > 0; i--)
                _curve[i - 1] = _curve[i] - _slope[i - 1] * step;
        }

        private void PackLut()
        {
            var destination = _lutBytes.AsSpan();
            for (var i = 0; i < LutSize; i++)
            {
                var offset = i * 16;
                BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], _values[i]);
                BinaryPrimitives.WriteSingleLittleEndian(destination[(offset + 4)..], _values[LutSize + i]);
                BinaryPrimitives.WriteSingleLittleEndian(destination[(offset + 8)..], _values[LutSize * 2 + i]);
                BinaryPrimitives.WriteSingleLittleEndian(destination[(offset + 12)..], 0f);
            }
        }

        private static float SrgbToLinear(float value)
        {
            var position = Math.Clamp(value, 0f, 1f) * (SrgbTableSize - 1);
            var index = (int)position;
            if (index >= SrgbTableSize - 1)
                return SrgbToLinearTable[SrgbTableSize - 1];

            var weight = position - index;
            return SrgbToLinearTable[index] * (1f - weight) + SrgbToLinearTable[index + 1] * weight;
        }

        private static float[] CreateSrgbToLinearTable()
        {
            var table = new float[SrgbTableSize];
            for (var i = 0; i < SrgbTableSize; i++)
            {
                var value = (float)i / (SrgbTableSize - 1);
                table[i] = value <= 0.04045f
                    ? value / 12.92f
                    : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
            }
            return table;
        }

        private void PackLocalDelta()
        {
            var destination = _localDeltaBytes.AsSpan();
            for (var cell = 0; cell < GridSize * GridSize; cell++)
            {
                var offset = cell * 16;
                var weight = _reference.GridWeight[cell];
                for (var channel = 0; channel < Channels; channel++)
                {
                    var delta = weight > 0.0
                        ? _reference.GridSum[cell * Channels + channel] / weight - _reference.Mean[channel]
                        : 0.0;
                    BinaryPrimitives.WriteSingleLittleEndian(destination[(offset + channel * 4)..], (float)delta);
                }
                BinaryPrimitives.WriteSingleLittleEndian(destination[(offset + 12)..], 0f);
            }
        }

        private static float[] CreateOpaqueLinearTable()
        {
            var table = new float[256];
            for (var i = 0; i < table.Length; i++)
                table[i] = SrgbToLinear(i * (1f / 255f));
            return table;
        }

        private sealed class Accumulator
        {
            public double Weight;

            public readonly double[] Mean = new double[Channels];
            public readonly double[] Deviation = new double[Channels];
            public readonly double[] Minimum = new double[Channels];
            public readonly double[] Maximum = new double[Channels];
            public readonly double[] GridWeight = new double[GridSize * GridSize];
            public readonly double[] GridSum = new double[GridSize * GridSize * Channels];

            private readonly double[] _sum = new double[Channels];
            private readonly double[] _square = new double[Channels];
            private readonly double[] _histogram = new double[Channels * Bins];
            private int[] _columnCell = [];
            private int _columnCellWidth;
            private readonly double[] _cumulated = new double[Channels * (Bins + 1)];
            private readonly double[] _scratch = new double[Bins];

            public void Reset()
            {
                Weight = 0.0;
                Array.Clear(_sum);
                Array.Clear(_square);
                Array.Clear(_histogram);
                Array.Clear(GridWeight);
                Array.Clear(GridSum);
                for (var channel = 0; channel < Channels; channel++)
                {
                    Minimum[channel] = double.MaxValue;
                    Maximum[channel] = double.MinValue;
                }
            }

            public void Accumulate(ReadOnlySpan<int> pixels, int width, int height, bool useHistogram)
            {
                var useGrid = width > 0 && height > 0 && pixels.Length == width * height;
                if (useGrid)
                    EnsureColumnCells(width);

                var gridWeight = GridWeight;
                var gridSum = GridSum;
                var columnCell = _columnCell;
                var columns = useGrid ? width : pixels.Length;
                var column = 0;
                var rowIndex = 0;
                var cellRow = 0;

                var weightSum = Weight;
                var sum0 = _sum[0];
                var sum1 = _sum[1];
                var sum2 = _sum[2];
                var square0 = _square[0];
                var square1 = _square[1];
                var square2 = _square[2];
                var minimum0 = Minimum[0];
                var minimum1 = Minimum[1];
                var minimum2 = Minimum[2];
                var maximum0 = Maximum[0];
                var maximum1 = Maximum[1];
                var maximum2 = Maximum[2];

                var histogram = _histogram;
                var opaque = OpaqueLinearTable;
                var low0 = BinMinimum[0];
                var low1 = BinMinimum[1];
                var low2 = BinMinimum[2];
                var span0 = BinMaximum[0] - low0;
                var span1 = BinMaximum[1] - low1;
                var span2 = BinMaximum[2] - low2;

                foreach (var pixel in pixels)
                {
                    var alpha = (int)((uint)pixel >> 24);
                    if (alpha >= MinimumAlpha)
                    {
                        float blue, green, red;
                        if (alpha == 255)
                        {
                            blue = opaque[pixel & 0xFF];
                            green = opaque[(pixel >> 8) & 0xFF];
                            red = opaque[(pixel >> 16) & 0xFF];
                        }
                        else
                        {
                            var inverse = 1f / alpha;
                            blue = SrgbToLinear((pixel & 0xFF) * inverse);
                            green = SrgbToLinear(((pixel >> 8) & 0xFF) * inverse);
                            red = SrgbToLinear(((pixel >> 16) & 0xFF) * inverse);
                        }

                        var l = 0.4122214708f * red + 0.5363325363f * green + 0.0514459929f * blue;
                        var m = 0.2119034982f * red + 0.6806995451f * green + 0.1073969566f * blue;
                        var s = 0.0883024619f * red + 0.2817188376f * green + 0.6299787005f * blue;

                        var lRoot = MathF.Cbrt(l);
                        var mRoot = MathF.Cbrt(m);
                        var sRoot = MathF.Cbrt(s);

                        var weight = alpha / 255.0;

                        double value0 = 0.2104542553f * lRoot + 0.7936177850f * mRoot - 0.0040720468f * sRoot;
                        sum0 += value0 * weight;
                        square0 += value0 * value0 * weight;
                        if (value0 < minimum0)
                            minimum0 = value0;
                        if (value0 > maximum0)
                            maximum0 = value0;

                        double value1 = 1.9779984951f * lRoot - 2.4285922050f * mRoot + 0.4505937099f * sRoot;
                        sum1 += value1 * weight;
                        square1 += value1 * value1 * weight;
                        if (value1 < minimum1)
                            minimum1 = value1;
                        if (value1 > maximum1)
                            maximum1 = value1;

                        double value2 = 0.0259040371f * lRoot + 0.7827717662f * mRoot - 0.8086757660f * sRoot;
                        sum2 += value2 * weight;
                        square2 += value2 * value2 * weight;
                        if (value2 < minimum2)
                            minimum2 = value2;
                        if (value2 > maximum2)
                            maximum2 = value2;

                        if (useHistogram)
                        {
                            histogram[Math.Clamp((int)((value0 - low0) / span0 * Bins), 0, Bins - 1)] += weight;
                            histogram[Bins + Math.Clamp((int)((value1 - low1) / span1 * Bins), 0, Bins - 1)] += weight;
                            histogram[Bins * 2 + Math.Clamp((int)((value2 - low2) / span2 * Bins), 0, Bins - 1)] += weight;
                        }

                        if (useGrid)
                        {
                            var cell = cellRow + columnCell[column];
                            gridWeight[cell] += weight;
                            gridSum[cell * Channels] += value0 * weight;
                            gridSum[cell * Channels + 1] += value1 * weight;
                            gridSum[cell * Channels + 2] += value2 * weight;
                        }

                        weightSum += weight;
                    }

                    if (++column == columns)
                    {
                        column = 0;
                        rowIndex++;
                        if (useGrid)
                            cellRow = Math.Min(rowIndex * GridSize / height, GridSize - 1) * GridSize;
                    }
                }

                Weight = weightSum;
                _sum[0] = sum0;
                _sum[1] = sum1;
                _sum[2] = sum2;
                _square[0] = square0;
                _square[1] = square1;
                _square[2] = square2;
                Minimum[0] = minimum0;
                Minimum[1] = minimum1;
                Minimum[2] = minimum2;
                Maximum[0] = maximum0;
                Maximum[1] = maximum1;
                Maximum[2] = maximum2;
            }

            public void Resolve(bool useHistogram)
            {
                for (var channel = 0; channel < Channels; channel++)
                {
                    var mean = _sum[channel] / Weight;
                    var variance = _square[channel] / Weight - mean * mean;
                    Mean[channel] = mean;
                    Deviation[channel] = Math.Sqrt(Math.Max(variance, 0.0));

                    if (Minimum[channel] > Maximum[channel])
                    {
                        Minimum[channel] = mean;
                        Maximum[channel] = mean;
                    }

                    if (useHistogram)
                    {
                        Smooth(channel);
                        Cumulate(channel);
                    }
                }
            }

            private void EnsureColumnCells(int width)
            {
                if (_columnCellWidth == width)
                    return;

                if (_columnCell.Length < width)
                    _columnCell = new int[width];
                for (var x = 0; x < width; x++)
                    _columnCell[x] = Math.Min(x * GridSize / width, GridSize - 1);
                _columnCellWidth = width;
            }

            public double Cumulative(int channel, double value)
            {
                var low = BinMinimum[channel];
                var span = BinMaximum[channel] - low;
                var position = Math.Clamp((value - low) / span * Bins, 0.0, Bins);
                var index = Math.Min((int)position, Bins - 1);
                var weight = position - index;
                var offset = channel * (Bins + 1);
                return _cumulated[offset + index] * (1.0 - weight) + _cumulated[offset + index + 1] * weight;
            }

            public double Quantile(int channel, double probability)
            {
                var offset = channel * (Bins + 1);
                var target = Math.Clamp(probability, 0.0, 1.0);

                var index = 0;
                while (index < Bins - 1 && _cumulated[offset + index + 1] < target)
                    index++;

                var low = _cumulated[offset + index];
                var high = _cumulated[offset + index + 1];
                var weight = high > low ? (target - low) / (high - low) : 0.5;

                var binLow = BinMinimum[channel];
                var binSpan = BinMaximum[channel] - binLow;
                return binLow + (index + weight) / Bins * binSpan;
            }

            private void Smooth(int channel)
            {
                var offset = channel * Bins;
                for (var pass = 0; pass < SmoothingPasses; pass++)
                {
                    for (var i = 0; i < Bins; i++)
                    {
                        var previous = _histogram[offset + Math.Max(i - 1, 0)];
                        var current = _histogram[offset + i];
                        var next = _histogram[offset + Math.Min(i + 1, Bins - 1)];
                        _scratch[i] = (previous + current * 2.0 + next) * 0.25;
                    }
                    _scratch.AsSpan().CopyTo(_histogram.AsSpan(offset, Bins));
                }
            }

            private void Cumulate(int channel)
            {
                var histogramOffset = channel * Bins;
                var cumulatedOffset = channel * (Bins + 1);

                var total = 0.0;
                _cumulated[cumulatedOffset] = 0.0;
                for (var i = 0; i < Bins; i++)
                {
                    total += _histogram[histogramOffset + i];
                    _cumulated[cumulatedOffset + i + 1] = total;
                }

                if (total <= 0.0)
                {
                    for (var i = 0; i <= Bins; i++)
                        _cumulated[cumulatedOffset + i] = (double)i / Bins;
                    return;
                }

                var inverse = 1.0 / total;
                for (var i = 0; i <= Bins; i++)
                    _cumulated[cumulatedOffset + i] *= inverse;
            }
        }
    }
}
