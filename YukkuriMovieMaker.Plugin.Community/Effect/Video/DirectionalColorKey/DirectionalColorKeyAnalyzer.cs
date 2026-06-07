using System.Numerics;
using ComputeSharp;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    internal sealed class DirectionalColorKeyAnalyzer : IDisposable
    {
        private readonly GraphicsDevice device;

        private ReadOnlyBuffer<int>? bgraBuffer;
        private ReadWriteBuffer<int>? previousBgraBuffer;
        private ReadWriteBuffer<float>? colorLabBuffer;
        private ReadWriteBuffer<float>? directionBufferA;
        private ReadWriteBuffer<float>? directionBufferB;
        private ReadWriteBuffer<float>? previousResultBuffer;
        private ReadWriteBuffer<int>? maskBufferA;
        private ReadWriteBuffer<int>? maskBufferB;
        private ReadWriteBuffer<int>? adoptMaskBuffer;
        private ReadWriteBuffer<int>? computeMaskBuffer;
        private ReadOnlyBuffer<float>? centerBuffer;
        private ReadWriteBuffer<int>? sumBuffer;
        private ReadWriteBuffer<int>? countBuffer;
        private ReadWriteBuffer<int>? histogramBuffer;

        private int width;
        private int height;
        private int pixelCount;

        private const int MaxClusters = 4;
        private const int SmoothRadius = 4;
        private const int SmoothIterations = 5;
        private const int LloydIterations = 12;
        private const int ProjectionBins = 256;
        private const float FixedPointScale = 64f;
        private const float ProjectionHistogramRange = 1.0f;
        private const float LambdaSmoothingAlpha = 0.25f;
        private const int AdoptReach = SmoothRadius * SmoothIterations;
        private const int GuardReach = SmoothRadius * SmoothIterations;
        private const float IncrementalChangeCeiling = 0.25f;

        private readonly float[] centers = new float[MaxClusters * 3];
        private readonly int[] sums = new int[MaxClusters * 3];
        private readonly int[] counts = new int[MaxClusters];
        private readonly int[] histogram = new int[MaxClusters * ProjectionBins];
        private readonly float[] lambdas = new float[MaxClusters];
        private readonly float[] prevLambdas = new float[MaxClusters];
        private readonly int[] zeroSums = new int[MaxClusters * 3];
        private readonly int[] zeroCounts = new int[MaxClusters];
        private readonly int[] zeroHistogram = new int[MaxClusters * ProjectionBins];

        private int clusterCount = 1;
        private bool hasWarmStart;
        private bool hasLambdaWarmStart;
        private bool hasPreviousResult;
        private int lastNoiseThresholdBits;
        private int lastSigmaColorBits;
        private int lastBackgroundLabXBits;
        private int lastBackgroundLabYBits;
        private int lastBackgroundLabZBits;

        public DirectionalColorKeyAnalyzer()
        {
            device = GraphicsDevice.GetDefault();
        }

        public int ClusterCount => clusterCount;

        public Vector3 GetCenter(int cluster)
            => new(centers[cluster * 3 + 0], centers[cluster * 3 + 1], centers[cluster * 3 + 2]);

        public float GetLambda(int cluster) => lambdas[cluster];

        public void Analyze(
            ReadOnlySpan<int> bgra,
            int width,
            int height,
            Vector3 backgroundLab,
            Vector3 whiteDirection,
            int requestedClusters,
            float noiseThreshold,
            float sigmaColor,
            DirectionalColorKeyScaleMode scaleMode,
            float opaquePercentile,
            float foregroundLambda,
            Func<Vector3, float, float> physicalLambda)
        {
            EnsureCapacity(width, height);

            int targetClusters = Math.Clamp(requestedClusters, 1, MaxClusters);
            int noiseThresholdBits = BitConverter.SingleToInt32Bits(noiseThreshold);
            int sigmaColorBits = BitConverter.SingleToInt32Bits(sigmaColor);
            int backgroundLabXBits = BitConverter.SingleToInt32Bits(backgroundLab.X);
            int backgroundLabYBits = BitConverter.SingleToInt32Bits(backgroundLab.Y);
            int backgroundLabZBits = BitConverter.SingleToInt32Bits(backgroundLab.Z);

            var bgraGpu = EnsureBgraBuffer();
            var colorLabGpu = EnsureColorLabBuffer();
            var directionGpu = EnsureDirectionBufferA();
            var directionScratch = EnsureDirectionBufferB();

            bgraGpu.CopyFrom(bgra[..pixelCount]);

            device.For(width, height, new DisplacementFieldShader(
                bgraGpu, colorLabGpu, directionGpu,
                backgroundLab.X, backgroundLab.Y, backgroundLab.Z,
                noiseThreshold, width, height));

            float sigmaColorSq = 2f * sigmaColor * sigmaColor;

            bool canReuse = hasPreviousResult
                && noiseThresholdBits == lastNoiseThresholdBits
                && sigmaColorBits == lastSigmaColorBits
                && backgroundLabXBits == lastBackgroundLabXBits
                && backgroundLabYBits == lastBackgroundLabYBits
                && backgroundLabZBits == lastBackgroundLabZBits;

            ReadWriteBuffer<float> smoothedDirections;

            if (!canReuse || !TryRunIncrementalSmooth(
                bgra, directionGpu, directionScratch, colorLabGpu, sigmaColorSq, out smoothedDirections))
            {
                var smoothSource = directionGpu;
                var smoothTarget = directionScratch;

                for (int iteration = 0; iteration < SmoothIterations; iteration++)
                {
                    device.For(width, height, new DirectionSmoothShader(
                        smoothSource, colorLabGpu, smoothTarget, sigmaColorSq, SmoothRadius, width, height));
                    (smoothSource, smoothTarget) = (smoothTarget, smoothSource);
                }

                smoothedDirections = smoothSource;
            }

            device.For(width, height, new CopyDirectionsShader(
                smoothedDirections, EnsurePreviousResultBuffer(), width, height));
            EnsurePreviousBgraBuffer().CopyFrom(bgra[..pixelCount]);
            hasPreviousResult = true;
            lastNoiseThresholdBits = noiseThresholdBits;
            lastSigmaColorBits = sigmaColorBits;
            lastBackgroundLabXBits = backgroundLabXBits;
            lastBackgroundLabYBits = backgroundLabYBits;
            lastBackgroundLabZBits = backgroundLabZBits;

            InitializeCenters(targetClusters, whiteDirection);

            var centerGpu = EnsureCenterBuffer();
            var sumGpu = EnsureSumBuffer();
            var countGpu = EnsureCountBuffer();

            for (int iteration = 0; iteration < LloydIterations; iteration++)
            {
                centerGpu.CopyFrom(centers.AsSpan(0, clusterCount * 3));
                sumGpu.CopyFrom(zeroSums.AsSpan(0, clusterCount * 3));
                countGpu.CopyFrom(zeroCounts.AsSpan(0, clusterCount));

                device.For(width, height, new ClusterAssignAccumulateShader(
                    smoothedDirections, centerGpu, sumGpu, countGpu, clusterCount, FixedPointScale, width, height));

                sumGpu.CopyTo(sums.AsSpan(0, clusterCount * 3));
                countGpu.CopyTo(counts.AsSpan(0, clusterCount));

                bool converged = UpdateCenters(whiteDirection);
                if (converged)
                    break;
            }

            ComputeLambdas(colorLabGpu, smoothedDirections, backgroundLab, scaleMode, opaquePercentile, foregroundLambda, physicalLambda);

            if (hasLambdaWarmStart)
            {
                for (int c = 0; c < clusterCount; c++)
                    lambdas[c] = prevLambdas[c] + (lambdas[c] - prevLambdas[c]) * LambdaSmoothingAlpha;
            }
            Array.Copy(lambdas, prevLambdas, clusterCount);
            hasLambdaWarmStart = true;

            hasWarmStart = true;
        }

        private bool TryRunIncrementalSmooth(
            ReadOnlySpan<int> bgra,
            ReadWriteBuffer<float> rawDirections,
            ReadWriteBuffer<float> scratchDirections,
            ReadWriteBuffer<float> colorLabGpu,
            float sigmaColorSq,
            out ReadWriteBuffer<float> smoothedDirections)
        {
            var bgraGpu = EnsureBgraBuffer();
            var previousBgraGpu = EnsurePreviousBgraBuffer();
            var seedScratch = EnsureMaskBufferA();
            var dilateScratch = EnsureMaskBufferB();
            var adoptMask = EnsureAdoptMaskBuffer();
            var computeMask = EnsureComputeMaskBuffer();
            var countGpu = EnsureCountBuffer();

            device.For(width, height, new ChangeSeedShader(
                bgraGpu, previousBgraGpu, seedScratch, width, height));

            countGpu.CopyFrom(zeroCounts.AsSpan(0, 1));
            device.For(width, height, new MaskCountShader(seedScratch, countGpu, width, height));
            countGpu.CopyTo(counts.AsSpan(0, 1));

            if (counts[0] > (int)(pixelCount * IncrementalChangeCeiling))
            {
                smoothedDirections = scratchDirections;
                return false;
            }

            device.For(width, height, new DilateHorizontalShader(seedScratch, dilateScratch, AdoptReach, width, height));
            device.For(width, height, new DilateVerticalShader(dilateScratch, adoptMask, AdoptReach, width, height));

            device.For(width, height, new DilateHorizontalShader(adoptMask, dilateScratch, GuardReach, width, height));
            device.For(width, height, new DilateVerticalShader(dilateScratch, computeMask, GuardReach, width, height));

            var smoothSource = rawDirections;
            var smoothTarget = scratchDirections;

            for (int iteration = 0; iteration < SmoothIterations; iteration++)
            {
                device.For(width, height, new RegionDirectionSmoothShader(
                    smoothSource, colorLabGpu, smoothTarget, computeMask, sigmaColorSq, SmoothRadius, width, height));
                (smoothSource, smoothTarget) = (smoothTarget, smoothSource);
            }

            device.For(width, height, new AdoptRegionShader(
                smoothSource, EnsurePreviousResultBuffer(), adoptMask, width, height));

            smoothedDirections = smoothSource;
            return true;
        }

        private void InitializeCenters(int targetClusters, Vector3 whiteDirection)
        {
            Vector3 primary = Normalize(whiteDirection, new Vector3(1f, 0f, 0f));

            if (!hasWarmStart || clusterCount != targetClusters)
            {
                clusterCount = targetClusters;
                hasLambdaWarmStart = false;
                SetCenter(0, primary);

                for (int c = 1; c < clusterCount; c++)
                {
                    float angle = MathF.PI * c / clusterCount;
                    var perturbed = Normalize(new Vector3(
                        primary.X,
                        primary.Y * MathF.Cos(angle) - primary.Z * MathF.Sin(angle),
                        primary.Y * MathF.Sin(angle) + primary.Z * MathF.Cos(angle)), primary);
                    SetCenter(c, perturbed);
                }
            }
        }

        private bool UpdateCenters(Vector3 whiteDirection)
        {
            const float convergenceDot = 0.999995f;
            bool converged = true;
            Vector3 fallback = Normalize(whiteDirection, new Vector3(1f, 0f, 0f));

            for (int c = 0; c < clusterCount; c++)
            {
                if (counts[c] == 0)
                {
                    SetCenter(c, fallback);
                    converged = false;
                    continue;
                }

                var accumulated = new Vector3(
                    sums[c * 3 + 0] / FixedPointScale,
                    sums[c * 3 + 1] / FixedPointScale,
                    sums[c * 3 + 2] / FixedPointScale);

                var previous = GetCenter(c);
                var updated = Normalize(accumulated, previous);
                SetCenter(c, updated);

                if (Vector3.Dot(updated, previous) < convergenceDot)
                    converged = false;
            }

            return converged;
        }

        private void ComputeLambdas(
            ReadWriteBuffer<float> colorLabGpu,
            ReadWriteBuffer<float> directionGpu,
            Vector3 backgroundLab,
            DirectionalColorKeyScaleMode scaleMode,
            float opaquePercentile,
            float foregroundLambda,
            Func<Vector3, float, float> physicalLambda)
        {
            if (scaleMode == DirectionalColorKeyScaleMode.Foreground)
            {
                for (int c = 0; c < clusterCount; c++)
                    lambdas[c] = MathF.Max(foregroundLambda, 1e-5f);
                return;
            }

            if (scaleMode == DirectionalColorKeyScaleMode.Physical)
            {
                for (int c = 0; c < clusterCount; c++)
                    lambdas[c] = physicalLambda(GetCenter(c), 1e-5f);
                return;
            }

            var centerGpu = EnsureCenterBuffer();
            var histogramGpu = EnsureHistogramBuffer();

            centerGpu.CopyFrom(centers.AsSpan(0, clusterCount * 3));
            histogramGpu.CopyFrom(zeroHistogram.AsSpan(0, clusterCount * ProjectionBins));

            float projectionScale = ProjectionBins / ProjectionHistogramRange;

            device.For(width, height, new ProjectionHistogramShader(
                colorLabGpu, directionGpu, centerGpu, histogramGpu,
                backgroundLab.X, backgroundLab.Y, backgroundLab.Z,
                clusterCount, ProjectionBins, projectionScale, width, height));

            histogramGpu.CopyTo(histogram.AsSpan(0, clusterCount * ProjectionBins));

            float fraction = Math.Clamp(opaquePercentile, 0f, 1f);

            for (int c = 0; c < clusterCount; c++)
            {
                int baseIndex = c * ProjectionBins;
                long total = 0;
                for (int b = 0; b < ProjectionBins; b++)
                    total += histogram[baseIndex + b];

                if (total == 0)
                {
                    lambdas[c] = physicalLambda(GetCenter(c), 1e-5f);
                    continue;
                }

                long target = (long)(total * fraction);
                long cumulative = 0;
                int selectedBin = ProjectionBins - 1;
                for (int b = 0; b < ProjectionBins; b++)
                {
                    cumulative += histogram[baseIndex + b];
                    if (cumulative >= target)
                    {
                        selectedBin = b;
                        break;
                    }
                }

                float projValue = (selectedBin + 0.5f) / projectionScale;
                lambdas[c] = MathF.Max(projValue, 1e-5f);
            }
        }

        private void SetCenter(int cluster, Vector3 value)
        {
            centers[cluster * 3 + 0] = value.X;
            centers[cluster * 3 + 1] = value.Y;
            centers[cluster * 3 + 2] = value.Z;
        }

        private static Vector3 Normalize(Vector3 value, Vector3 fallback)
        {
            float length = value.Length();
            return length > 1e-6f ? value / length : fallback;
        }

        private void EnsureCapacity(int width, int height)
        {
            if (this.width == width && this.height == height)
                return;

            this.width = width;
            this.height = height;
            pixelCount = width * height;

            hasPreviousResult = false;
            DisposeFrameBuffers();
        }

        private ReadOnlyBuffer<int> EnsureBgraBuffer()
        {
            if (bgraBuffer is null || bgraBuffer.Length < pixelCount)
            {
                bgraBuffer?.Dispose();
                bgraBuffer = device.AllocateReadOnlyBuffer<int>(pixelCount);
            }
            return bgraBuffer;
        }

        private ReadWriteBuffer<int> EnsurePreviousBgraBuffer()
        {
            if (previousBgraBuffer is null || previousBgraBuffer.Length < pixelCount)
            {
                previousBgraBuffer?.Dispose();
                previousBgraBuffer = device.AllocateReadWriteBuffer<int>(pixelCount);
            }
            return previousBgraBuffer;
        }

        private ReadWriteBuffer<float> EnsurePreviousResultBuffer()
        {
            if (previousResultBuffer is null || previousResultBuffer.Length < pixelCount * 3)
            {
                previousResultBuffer?.Dispose();
                previousResultBuffer = device.AllocateReadWriteBuffer<float>(pixelCount * 3);
            }
            return previousResultBuffer;
        }

        private ReadWriteBuffer<int> EnsureMaskBufferA()
        {
            if (maskBufferA is null || maskBufferA.Length < pixelCount)
            {
                maskBufferA?.Dispose();
                maskBufferA = device.AllocateReadWriteBuffer<int>(pixelCount);
            }
            return maskBufferA;
        }

        private ReadWriteBuffer<int> EnsureMaskBufferB()
        {
            if (maskBufferB is null || maskBufferB.Length < pixelCount)
            {
                maskBufferB?.Dispose();
                maskBufferB = device.AllocateReadWriteBuffer<int>(pixelCount);
            }
            return maskBufferB;
        }

        private ReadWriteBuffer<int> EnsureAdoptMaskBuffer()
        {
            if (adoptMaskBuffer is null || adoptMaskBuffer.Length < pixelCount)
            {
                adoptMaskBuffer?.Dispose();
                adoptMaskBuffer = device.AllocateReadWriteBuffer<int>(pixelCount);
            }
            return adoptMaskBuffer;
        }

        private ReadWriteBuffer<int> EnsureComputeMaskBuffer()
        {
            if (computeMaskBuffer is null || computeMaskBuffer.Length < pixelCount)
            {
                computeMaskBuffer?.Dispose();
                computeMaskBuffer = device.AllocateReadWriteBuffer<int>(pixelCount);
            }
            return computeMaskBuffer;
        }

        private ReadWriteBuffer<float> EnsureColorLabBuffer()
        {
            if (colorLabBuffer is null || colorLabBuffer.Length < pixelCount * 3)
            {
                colorLabBuffer?.Dispose();
                colorLabBuffer = device.AllocateReadWriteBuffer<float>(pixelCount * 3);
            }
            return colorLabBuffer;
        }

        private ReadWriteBuffer<float> EnsureDirectionBufferA()
        {
            if (directionBufferA is null || directionBufferA.Length < pixelCount * 3)
            {
                directionBufferA?.Dispose();
                directionBufferA = device.AllocateReadWriteBuffer<float>(pixelCount * 3);
            }
            return directionBufferA;
        }

        private ReadWriteBuffer<float> EnsureDirectionBufferB()
        {
            if (directionBufferB is null || directionBufferB.Length < pixelCount * 3)
            {
                directionBufferB?.Dispose();
                directionBufferB = device.AllocateReadWriteBuffer<float>(pixelCount * 3);
            }
            return directionBufferB;
        }

        private ReadOnlyBuffer<float> EnsureCenterBuffer()
        {
            centerBuffer ??= device.AllocateReadOnlyBuffer<float>(MaxClusters * 3);
            return centerBuffer;
        }

        private ReadWriteBuffer<int> EnsureSumBuffer()
        {
            sumBuffer ??= device.AllocateReadWriteBuffer<int>(MaxClusters * 3);
            return sumBuffer;
        }

        private ReadWriteBuffer<int> EnsureCountBuffer()
        {
            countBuffer ??= device.AllocateReadWriteBuffer<int>(MaxClusters);
            return countBuffer;
        }

        private ReadWriteBuffer<int> EnsureHistogramBuffer()
        {
            histogramBuffer ??= device.AllocateReadWriteBuffer<int>(MaxClusters * ProjectionBins);
            return histogramBuffer;
        }

        private void DisposeFrameBuffers()
        {
            bgraBuffer?.Dispose();
            previousBgraBuffer?.Dispose();
            colorLabBuffer?.Dispose();
            directionBufferA?.Dispose();
            directionBufferB?.Dispose();
            previousResultBuffer?.Dispose();
            maskBufferA?.Dispose();
            maskBufferB?.Dispose();
            adoptMaskBuffer?.Dispose();
            computeMaskBuffer?.Dispose();
            bgraBuffer = null;
            previousBgraBuffer = null;
            colorLabBuffer = null;
            directionBufferA = null;
            directionBufferB = null;
            previousResultBuffer = null;
            maskBufferA = null;
            maskBufferB = null;
            adoptMaskBuffer = null;
            computeMaskBuffer = null;
        }

        public void Dispose()
        {
            DisposeFrameBuffers();
            centerBuffer?.Dispose();
            sumBuffer?.Dispose();
            countBuffer?.Dispose();
            histogramBuffer?.Dispose();
            centerBuffer = null;
            sumBuffer = null;
            countBuffer = null;
            histogramBuffer = null;
        }
    }
}
