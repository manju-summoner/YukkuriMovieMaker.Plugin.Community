using System.Numerics;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons.Compute;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    internal sealed class DirectionalColorKeyAnalyzer : IDisposable
    {
        private readonly ComputeShaderDevice device;
        private readonly ComputeConstantBuffer constants;

        private ComputeBuffer<int>? bgraBuffer;
        private ComputeBuffer<int>? previousBgraBuffer;
        private ComputeBuffer<float>? colorLabBuffer;
        private ComputeBuffer<float>? directionBufferA;
        private ComputeBuffer<float>? directionBufferB;
        private ComputeBuffer<float>? previousResultBuffer;
        private ComputeBuffer<int>? maskBufferA;
        private ComputeBuffer<int>? maskBufferB;
        private ComputeBuffer<int>? adoptMaskBuffer;
        private ComputeBuffer<int>? computeMaskBuffer;
        private ComputeBuffer<float>? centerBuffer;
        private ComputeBuffer<int>? accumBuffer;
        private ComputeBuffer<int>? countBuffer;
        private ComputeBuffer<int>? histogramBuffer;
        private ComputeBuffer<int>? foregroundBufferA;
        private ComputeBuffer<int>? foregroundBufferB;
        private ComputeBuffer<int>? validBufferA;
        private ComputeBuffer<int>? validBufferB;
        private int[]? foregroundReadback;

        private int width;
        private int height;
        private int pixelCount;

        private const int MaxClusters = 4;
        private const int SmoothRadius = DirectionSmoothConstants.Radius;
        private const int SmoothIterations = 5;
        private const int LloydIterations = 12;
        private const int ProjectionBins = 256;
        private const float FixedPointScale = 64f;
        private const float ProjectionHistogramRange = 1.0f;
        private const float LambdaSmoothingAlpha = 0.25f;
        private const float ConvergenceDot = 0.999995f;
        private const int AdoptReach = SmoothRadius * SmoothIterations;
        private const int GuardReach = SmoothRadius * SmoothIterations;
        private const float IncrementalChangeCeiling = 0.25f;
        private const int PropagateReach = 4;
        private const int PropagateIterations = 16;
        private const float LineSigmaSquared = 0.1225f;

        private readonly float[] centers = new float[MaxClusters * 3];
        private readonly int[] accumulators = new int[MaxClusters * 3 + MaxClusters];
        private readonly int[] counts = new int[MaxClusters];
        private readonly int[] histogram = new int[MaxClusters * ProjectionBins];
        private readonly float[] lambdas = new float[MaxClusters];
        private readonly float[] prevLambdas = new float[MaxClusters];
        private readonly int[] zeroAccumulators = new int[MaxClusters * 3 + MaxClusters];
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

        private DirectionalColorKeyAnalyzer(IGraphicsDevicesAndContext devices)
        {
            device = new ComputeShaderDevice(devices);
            constants = new ComputeConstantBuffer(device, 64);
        }

        // cs_5_0 に対応しない環境（FeatureLevel 11 未満）では生成に失敗させ、
        // null を返して呼び出し側でエフェクトをパススルーさせる。
        public static DirectionalColorKeyAnalyzer? TryCreate(IGraphicsDevicesAndContext devices)
        {
            try
            {
                var analyzer = new DirectionalColorKeyAnalyzer(devices);
                if (analyzer.device.IsSupported)
                    return analyzer;

                analyzer.Dispose();
                return null;
            }
            catch
            {
                return null;
            }
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
            Func<Vector3, float, float> physicalLambda,
            bool resetLambdaSmoothing)
        {
            EnsureCapacity(width, height);

            // スケール決定に関わる設定が変わったときは、前回値との時間方向ブレンドを
            // 効かせると新しい設定値へ到達しないため warm-start を破棄する。
            // 再生によるコンテンツ変化のみのときは warm-start を維持してちらつきを抑える。
            if (resetLambdaSmoothing)
                hasLambdaWarmStart = false;

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

            bgraGpu.Upload(bgra[..pixelCount]);

            constants.Update(new DisplacementFieldConstants(width, height, 1, backgroundLab.X, backgroundLab.Y, backgroundLab.Z, noiseThreshold, width, height));
            device.Dispatch("DirectionalColorKeyDisplacementFieldCS", constants.Buffer, [bgraGpu.Srv], [colorLabGpu.Uav, directionGpu.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            float sigmaColorSq = 2f * sigmaColor * sigmaColor;

            bool canReuse = hasPreviousResult
                && noiseThresholdBits == lastNoiseThresholdBits
                && sigmaColorBits == lastSigmaColorBits
                && backgroundLabXBits == lastBackgroundLabXBits
                && backgroundLabYBits == lastBackgroundLabYBits
                && backgroundLabZBits == lastBackgroundLabZBits;

            ComputeBuffer<float> smoothedDirections;

            if (!canReuse || !TryRunIncrementalSmooth(
                bgra, directionGpu, directionScratch, colorLabGpu, sigmaColorSq, out smoothedDirections))
            {
                var smoothSource = directionGpu;
                var smoothTarget = directionScratch;

                for (int iteration = 0; iteration < SmoothIterations; iteration++)
                {
                    constants.Update(new DirectionSmoothConstantsBuffer(width, height, 1, sigmaColorSq, width, height));
                    device.Dispatch("DirectionalColorKeyDirectionSmoothCS", constants.Buffer, [], [smoothSource.Uav, colorLabGpu.Uav, smoothTarget.Uav],
                        ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
                    (smoothSource, smoothTarget) = (smoothTarget, smoothSource);
                }

                smoothedDirections = smoothSource;
            }

            constants.Update(new SizeConstants(width, height, 1, width, height));
            device.Dispatch("DirectionalColorKeyCopyDirectionsCS", constants.Buffer, [], [smoothedDirections.Uav, EnsurePreviousResultBuffer().Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
            EnsurePreviousBgraBuffer().Upload(bgra[..pixelCount]);
            hasPreviousResult = true;
            lastNoiseThresholdBits = noiseThresholdBits;
            lastSigmaColorBits = sigmaColorBits;
            lastBackgroundLabXBits = backgroundLabXBits;
            lastBackgroundLabYBits = backgroundLabYBits;
            lastBackgroundLabZBits = backgroundLabZBits;

            InitializeCenters(targetClusters, whiteDirection);

            var centerGpu = EnsureCenterBuffer();
            var accumGpu = EnsureAccumBuffer();

            int accumLength = clusterCount * 3 + clusterCount;

            for (int iteration = 0; iteration < LloydIterations; iteration++)
            {
                centerGpu.Upload(centers.AsSpan(0, clusterCount * 3));
                accumGpu.Upload(zeroAccumulators.AsSpan(0, accumLength));

                constants.Update(new ClusterAssignConstants(width, height, 1, clusterCount, FixedPointScale, width, height));
                device.Dispatch("DirectionalColorKeyClusterAssignAccumulateCS", constants.Buffer, [centerGpu.Srv], [smoothedDirections.Uav, accumGpu.Uav],
                    ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

                accumGpu.Readback(accumulators.AsSpan(0, accumLength));

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

        public ReadOnlySpan<int> BuildForegroundField(int width, int height, Vector3 backgroundLab, Vector3 backgroundSrgb)
        {
            EnsureCapacity(width, height);

            foregroundReadback ??= new int[pixelCount];
            if (foregroundReadback.Length < pixelCount)
                foregroundReadback = new int[pixelCount];

            float referencePerp = ComputeReferencePerp(backgroundLab);

            var bgraGpu = EnsureBgraBuffer();
            var colorLabGpu = EnsureColorLabBuffer();
            var foregroundSource = EnsureForegroundBufferA();
            var validSource = EnsureValidBufferA();
            var foregroundTarget = EnsureForegroundBufferB();
            var validTarget = EnsureValidBufferB();

            constants.Update(new ForegroundSeedConstants(width, height, 1, backgroundLab.X, backgroundLab.Y, backgroundLab.Z, referencePerp, width, height));
            device.Dispatch("DirectionalColorKeyForegroundSeedCS", constants.Buffer, [bgraGpu.Srv], [colorLabGpu.Uav, foregroundSource.Uav, validSource.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            for (int iteration = 0; iteration < PropagateIterations; iteration++)
            {
                constants.Update(new ForegroundPropagateConstants(width, height, 1, backgroundSrgb.X, backgroundSrgb.Y, backgroundSrgb.Z, PropagateReach, LineSigmaSquared, width, height));
                device.Dispatch("DirectionalColorKeyForegroundPropagateCS", constants.Buffer, [bgraGpu.Srv], [foregroundSource.Uav, validSource.Uav, foregroundTarget.Uav, validTarget.Uav],
                    ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

                (foregroundSource, foregroundTarget) = (foregroundTarget, foregroundSource);
                (validSource, validTarget) = (validTarget, validSource);
            }

            foregroundSource.Readback(foregroundReadback.AsSpan(0, pixelCount));
            return foregroundReadback.AsSpan(0, pixelCount);
        }

        private static float ComputeReferencePerp(Vector3 backgroundLab)
        {
            float bgLenSq = Vector3.Dot(backgroundLab, backgroundLab);
            if (bgLenSq <= 1e-8f)
                return 0f;

            var white = new Vector3(1f, 0f, 0f);
            var dvec = white - backgroundLab;
            float along = Vector3.Dot(dvec, backgroundLab) / bgLenSq;
            var perp = dvec - along * backgroundLab;
            return perp.Length();
        }

        private ComputeBuffer<int> EnsureForegroundBufferA()
        {
            if (foregroundBufferA is null || foregroundBufferA.Length < pixelCount)
            {
                foregroundBufferA?.Dispose();
                foregroundBufferA = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return foregroundBufferA;
        }

        private ComputeBuffer<int> EnsureForegroundBufferB()
        {
            if (foregroundBufferB is null || foregroundBufferB.Length < pixelCount)
            {
                foregroundBufferB?.Dispose();
                foregroundBufferB = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return foregroundBufferB;
        }

        private ComputeBuffer<int> EnsureValidBufferA()
        {
            if (validBufferA is null || validBufferA.Length < pixelCount)
            {
                validBufferA?.Dispose();
                validBufferA = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return validBufferA;
        }

        private ComputeBuffer<int> EnsureValidBufferB()
        {
            if (validBufferB is null || validBufferB.Length < pixelCount)
            {
                validBufferB?.Dispose();
                validBufferB = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return validBufferB;
        }

        private bool TryRunIncrementalSmooth(
            ReadOnlySpan<int> bgra,
            ComputeBuffer<float> rawDirections,
            ComputeBuffer<float> scratchDirections,
            ComputeBuffer<float> colorLabGpu,
            float sigmaColorSq,
            out ComputeBuffer<float> smoothedDirections)
        {
            var bgraGpu = EnsureBgraBuffer();
            var previousBgraGpu = EnsurePreviousBgraBuffer();
            var seedScratch = EnsureMaskBufferA();
            var dilateScratch = EnsureMaskBufferB();
            var adoptMask = EnsureAdoptMaskBuffer();
            var computeMask = EnsureComputeMaskBuffer();
            var countGpu = EnsureCountBuffer();

            constants.Update(new SizeConstants(width, height, 1, width, height));
            device.Dispatch("DirectionalColorKeyChangeSeedCS", constants.Buffer, [bgraGpu.Srv], [previousBgraGpu.Uav, seedScratch.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            countGpu.Upload(zeroCounts.AsSpan(0, 1));
            constants.Update(new SizeConstants(width, height, 1, width, height));
            device.Dispatch("DirectionalColorKeyMaskCountCS", constants.Buffer, [], [seedScratch.Uav, countGpu.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
            countGpu.Readback(counts.AsSpan(0, 1));

            if (counts[0] > (int)(pixelCount * IncrementalChangeCeiling))
            {
                smoothedDirections = scratchDirections;
                return false;
            }

            constants.Update(new DilateConstants(width, height, 1, AdoptReach, width, height));
            device.Dispatch("DirectionalColorKeyDilateHorizontalCS", constants.Buffer, [], [seedScratch.Uav, dilateScratch.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
            constants.Update(new DilateConstants(width, height, 1, AdoptReach, width, height));
            device.Dispatch("DirectionalColorKeyDilateVerticalCS", constants.Buffer, [], [dilateScratch.Uav, adoptMask.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            constants.Update(new DilateConstants(width, height, 1, GuardReach, width, height));
            device.Dispatch("DirectionalColorKeyDilateHorizontalCS", constants.Buffer, [], [adoptMask.Uav, dilateScratch.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
            constants.Update(new DilateConstants(width, height, 1, GuardReach, width, height));
            device.Dispatch("DirectionalColorKeyDilateVerticalCS", constants.Buffer, [], [dilateScratch.Uav, computeMask.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            var smoothSource = rawDirections;
            var smoothTarget = scratchDirections;

            for (int iteration = 0; iteration < SmoothIterations; iteration++)
            {
                constants.Update(new DirectionSmoothConstantsBuffer(width, height, 1, sigmaColorSq, width, height));
                device.Dispatch("DirectionalColorKeyRegionDirectionSmoothCS", constants.Buffer, [], [smoothSource.Uav, colorLabGpu.Uav, smoothTarget.Uav, computeMask.Uav],
                    ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));
                (smoothSource, smoothTarget) = (smoothTarget, smoothSource);
            }

            constants.Update(new SizeConstants(width, height, 1, width, height));
            device.Dispatch("DirectionalColorKeyAdoptRegionCS", constants.Buffer, [], [smoothSource.Uav, EnsurePreviousResultBuffer().Uav, adoptMask.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

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
                    SetCenter(c, PerturbedCenter(c, primary));

                return;
            }

            for (int c = 1; c < clusterCount; c++)
            {
                var current = GetCenter(c);
                for (int other = 0; other < c; other++)
                {
                    if (Vector3.Dot(current, GetCenter(other)) > ConvergenceDot)
                    {
                        SetCenter(c, PerturbedCenter(c, primary));
                        break;
                    }
                }
            }
        }

        private Vector3 PerturbedCenter(int cluster, Vector3 primary)
        {
            float angle = MathF.PI * cluster / clusterCount;
            return Normalize(new Vector3(
                primary.X,
                primary.Y * MathF.Cos(angle) - primary.Z * MathF.Sin(angle),
                primary.Y * MathF.Sin(angle) + primary.Z * MathF.Cos(angle)), primary);
        }

        private bool UpdateCenters(Vector3 whiteDirection)
        {
            bool converged = true;
            Vector3 fallback = Normalize(whiteDirection, new Vector3(1f, 0f, 0f));
            int countBase = clusterCount * 3;

            for (int c = 0; c < clusterCount; c++)
            {
                if (accumulators[countBase + c] == 0)
                {
                    SetCenter(c, fallback);
                    converged = false;
                    continue;
                }

                var accumulated = new Vector3(
                    accumulators[c * 3 + 0] / FixedPointScale,
                    accumulators[c * 3 + 1] / FixedPointScale,
                    accumulators[c * 3 + 2] / FixedPointScale);

                var previous = GetCenter(c);
                var updated = Normalize(accumulated, previous);
                SetCenter(c, updated);

                if (Vector3.Dot(updated, previous) < ConvergenceDot)
                    converged = false;
            }

            return converged;
        }

        private void ComputeLambdas(
            ComputeBuffer<float> colorLabGpu,
            ComputeBuffer<float> directionGpu,
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

            centerGpu.Upload(centers.AsSpan(0, clusterCount * 3));
            histogramGpu.Upload(zeroHistogram.AsSpan(0, clusterCount * ProjectionBins));

            float projectionScale = ProjectionBins / ProjectionHistogramRange;

            constants.Update(new ProjectionHistogramConstants(width, height, 1, backgroundLab.X, backgroundLab.Y, backgroundLab.Z, clusterCount, ProjectionBins, projectionScale, width, height));
            device.Dispatch("DirectionalColorKeyProjectionHistogramCS", constants.Buffer, [centerGpu.Srv], [colorLabGpu.Uav, directionGpu.Uav, histogramGpu.Uav],
                ComputeShaderDevice.GroupCount(width, 8), ComputeShaderDevice.GroupCount(height, 8));

            histogramGpu.Readback(histogram.AsSpan(0, clusterCount * ProjectionBins));

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

        private ComputeBuffer<int> EnsureBgraBuffer()
        {
            if (bgraBuffer is null || bgraBuffer.Length < pixelCount)
            {
                bgraBuffer?.Dispose();
                bgraBuffer = new ComputeBuffer<int>(device, pixelCount, false);
            }
            return bgraBuffer;
        }

        private ComputeBuffer<int> EnsurePreviousBgraBuffer()
        {
            if (previousBgraBuffer is null || previousBgraBuffer.Length < pixelCount)
            {
                previousBgraBuffer?.Dispose();
                previousBgraBuffer = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return previousBgraBuffer;
        }

        private ComputeBuffer<float> EnsurePreviousResultBuffer()
        {
            if (previousResultBuffer is null || previousResultBuffer.Length < pixelCount * 3)
            {
                previousResultBuffer?.Dispose();
                previousResultBuffer = new ComputeBuffer<float>(device, pixelCount * 3, true);
            }
            return previousResultBuffer;
        }

        private ComputeBuffer<int> EnsureMaskBufferA()
        {
            if (maskBufferA is null || maskBufferA.Length < pixelCount)
            {
                maskBufferA?.Dispose();
                maskBufferA = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return maskBufferA;
        }

        private ComputeBuffer<int> EnsureMaskBufferB()
        {
            if (maskBufferB is null || maskBufferB.Length < pixelCount)
            {
                maskBufferB?.Dispose();
                maskBufferB = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return maskBufferB;
        }

        private ComputeBuffer<int> EnsureAdoptMaskBuffer()
        {
            if (adoptMaskBuffer is null || adoptMaskBuffer.Length < pixelCount)
            {
                adoptMaskBuffer?.Dispose();
                adoptMaskBuffer = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return adoptMaskBuffer;
        }

        private ComputeBuffer<int> EnsureComputeMaskBuffer()
        {
            if (computeMaskBuffer is null || computeMaskBuffer.Length < pixelCount)
            {
                computeMaskBuffer?.Dispose();
                computeMaskBuffer = new ComputeBuffer<int>(device, pixelCount, true);
            }
            return computeMaskBuffer;
        }

        private ComputeBuffer<float> EnsureColorLabBuffer()
        {
            if (colorLabBuffer is null || colorLabBuffer.Length < pixelCount * 3)
            {
                colorLabBuffer?.Dispose();
                colorLabBuffer = new ComputeBuffer<float>(device, pixelCount * 3, true);
            }
            return colorLabBuffer;
        }

        private ComputeBuffer<float> EnsureDirectionBufferA()
        {
            if (directionBufferA is null || directionBufferA.Length < pixelCount * 3)
            {
                directionBufferA?.Dispose();
                directionBufferA = new ComputeBuffer<float>(device, pixelCount * 3, true);
            }
            return directionBufferA;
        }

        private ComputeBuffer<float> EnsureDirectionBufferB()
        {
            if (directionBufferB is null || directionBufferB.Length < pixelCount * 3)
            {
                directionBufferB?.Dispose();
                directionBufferB = new ComputeBuffer<float>(device, pixelCount * 3, true);
            }
            return directionBufferB;
        }

        private ComputeBuffer<float> EnsureCenterBuffer()
        {
            centerBuffer ??= new ComputeBuffer<float>(device, MaxClusters * 3, false);
            return centerBuffer;
        }

        private ComputeBuffer<int> EnsureAccumBuffer()
        {
            accumBuffer ??= new ComputeBuffer<int>(device, MaxClusters * 3 + MaxClusters, true);
            return accumBuffer;
        }

        private ComputeBuffer<int> EnsureCountBuffer()
        {
            countBuffer ??= new ComputeBuffer<int>(device, MaxClusters, true);
            return countBuffer;
        }

        private ComputeBuffer<int> EnsureHistogramBuffer()
        {
            histogramBuffer ??= new ComputeBuffer<int>(device, MaxClusters * ProjectionBins, true);
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
            foregroundBufferA?.Dispose();
            foregroundBufferB?.Dispose();
            validBufferA?.Dispose();
            validBufferB?.Dispose();
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
            foregroundBufferA = null;
            foregroundBufferB = null;
            validBufferA = null;
            validBufferB = null;
        }

        public void Dispose()
        {
            DisposeFrameBuffers();
            centerBuffer?.Dispose();
            accumBuffer?.Dispose();
            countBuffer?.Dispose();
            histogramBuffer?.Dispose();
            centerBuffer = null;
            accumBuffer = null;
            countBuffer = null;
            histogramBuffer = null;
            constants.Dispose();
            device.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct SizeConstants(int DispatchX, int DispatchY, int DispatchZ, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct DilateConstants(int DispatchX, int DispatchY, int DispatchZ, int Reach, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct DirectionSmoothConstantsBuffer(int DispatchX, int DispatchY, int DispatchZ, float SigmaColorSq, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct DisplacementFieldConstants(int DispatchX, int DispatchY, int DispatchZ, float BackgroundL, float BackgroundA, float BackgroundB, float NoiseThreshold, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct ClusterAssignConstants(int DispatchX, int DispatchY, int DispatchZ, int ClusterCount, float FixedPointScale, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct ProjectionHistogramConstants(int DispatchX, int DispatchY, int DispatchZ, float BackgroundL, float BackgroundA, float BackgroundB, int ClusterCount, int BinsPerCluster, float ProjectionScale, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct ForegroundSeedConstants(int DispatchX, int DispatchY, int DispatchZ, float BackgroundL, float BackgroundA, float BackgroundB, float ReferencePerp, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct ForegroundPropagateConstants(int DispatchX, int DispatchY, int DispatchZ, float BackgroundR, float BackgroundG, float BackgroundB, int Reach, float SigmaLineSq, int Width, int Height);
    }
}
