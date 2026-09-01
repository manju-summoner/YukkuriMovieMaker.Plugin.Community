using ComputeWeave;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    [ComputePipelineHost("device", 1)]
    internal sealed partial class DirectionalColorKeyPipelineHost
    {
        private readonly GraphicsDevice device;

        [ComputePipeline]
        private void RecordSrgbToLinearTable(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> table,
            int length)
        {
            _ = device;

            context.For(length, new SrgbToLinearTableShader(table));
            context.Barrier(table);
        }

        [ComputePipeline]
        private void RecordPremultipliedLinearTable(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> table,
            int length)
        {
            _ = device;

            context.For(length, new PremultipliedLinearTableShader(table));
            context.Barrier(table);
        }

        [ComputePipeline]
        private void RecordDisplacementField(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<int> bgra,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> colorLab,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> directions,
            float backgroundLabX,
            float backgroundLabY,
            float backgroundLabZ,
            float noiseThreshold,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new DisplacementFieldShader(
                bgra, colorLab, directions,
                backgroundLabX, backgroundLabY, backgroundLabZ,
                noiseThreshold, width, height));
            context.Barrier(colorLab);
            context.Barrier(directions);
        }

        [ComputePipeline]
        private void RecordChangeCount(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<int> bgra,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> previousBgra,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> seedMask,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> count,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new ChangeSeedShader(bgra, previousBgra, seedMask, width, height));
            context.Barrier(seedMask);

            context.For(width, height, new MaskCountShader(seedMask, count, width, height));
            context.Barrier(count);
        }

        [ComputePipeline]
        private void RecordDirectionSmooth(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> directions,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> scratch,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> colorLab,
            float sigmaColorSquared,
            int iterations,
            int width,
            int height)
        {
            _ = device;

            var source = directions;
            var target = scratch;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                context.For(width, height, new DirectionSmoothShader(source, colorLab, target, sigmaColorSquared, width, height));
                context.Barrier(target);

                (source, target) = (target, source);
            }
        }

        [ComputePipeline]
        private void RecordRegionSmooth(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> directions,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> scratch,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> colorLab,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> previousResult,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> seedMask,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> dilateScratch,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> adoptMask,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> computeMask,
            float sigmaColorSquared,
            int adoptReach,
            int guardReach,
            int iterations,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new DilateHorizontalShader(seedMask, dilateScratch, adoptReach, width, height));
            context.Barrier(dilateScratch);
            context.For(width, height, new DilateVerticalShader(dilateScratch, adoptMask, adoptReach, width, height));
            context.Barrier(adoptMask);

            context.For(width, height, new DilateHorizontalShader(adoptMask, dilateScratch, guardReach, width, height));
            context.Barrier(dilateScratch);
            context.For(width, height, new DilateVerticalShader(dilateScratch, computeMask, guardReach, width, height));
            context.Barrier(computeMask);

            var source = directions;
            var target = scratch;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                context.For(width, height, new RegionDirectionSmoothShader(
                    source, colorLab, target, computeMask, sigmaColorSquared, width, height));
                context.Barrier(target);

                (source, target) = (target, source);
            }

            context.For(width, height, new AdoptRegionShader(source, previousResult, adoptMask, width, height));
            context.Barrier(source);
        }

        [ComputePipeline]
        private void RecordPreviousSnapshot(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> smoothedDirections,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> previousResult,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<int> bgra,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> previousBgra,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new CopyDirectionsShader(smoothedDirections, previousResult, width, height));
            context.Barrier(previousResult);

            context.For(width, height, new CopyPackedShader(bgra, previousBgra, width, height));
            context.Barrier(previousBgra);
        }

        [ComputePipeline]
        private void RecordClusterAssign(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> directions,
            [ComputeResource(ComputeResourceAccess.Read)] ReadOnlyBuffer<float> centers,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> accumulators,
            int clusterCount,
            float fixedPointScale,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new ClusterAssignAccumulateShader(
                directions, centers, accumulators, clusterCount, fixedPointScale, width, height));
            context.Barrier(accumulators);
        }

        [ComputePipeline]
        private void RecordProjectionHistogram(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> colorLab,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> directions,
            [ComputeResource(ComputeResourceAccess.Read)] ReadOnlyBuffer<float> centers,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> histogram,
            float backgroundLabX,
            float backgroundLabY,
            float backgroundLabZ,
            int clusterCount,
            int binsPerCluster,
            float projectionScale,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new ProjectionHistogramShader(
                colorLab, directions, centers, histogram,
                backgroundLabX, backgroundLabY, backgroundLabZ,
                clusterCount, binsPerCluster, projectionScale, width, height));
            context.Barrier(histogram);
        }

        [ComputePipeline]
        private void RecordForegroundField(
            in ComputeContext context,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<int> bgra,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<float> colorLab,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<float> srgbToLinear,
            [ComputeResource(ComputeResourceAccess.Read)] IReadOnlyBuffer<float> premultipliedLinear,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> foregroundA,
            [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> foregroundB,
            float backgroundLabX,
            float backgroundLabY,
            float backgroundLabZ,
            float referencePerp,
            float backgroundSrgbR,
            float backgroundSrgbG,
            float backgroundSrgbB,
            float sigmaLineSquared,
            int iterations,
            int width,
            int height)
        {
            _ = device;

            context.For(width, height, new ForegroundSeedShader(
                bgra, colorLab, foregroundA,
                backgroundLabX, backgroundLabY, backgroundLabZ,
                referencePerp, width, height));
            context.Barrier(foregroundA);

            var source = foregroundA;
            var target = foregroundB;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                context.For(width, height, new ForegroundPropagateShader(
                    source, bgra, srgbToLinear, premultipliedLinear, target,
                    backgroundSrgbR, backgroundSrgbG, backgroundSrgbB,
                    sigmaLineSquared, width, height));
                context.Barrier(target);

                (source, target) = (target, source);
            }
        }
    }
}
