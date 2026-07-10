using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.FileSource;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.Bevel
{
    internal class BevelHeightmapSource : IVideoEffectProcessor
    {
        const int MaxThickness = 500;
        const int MaxJumpPasses = 10;

        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly BevelHeightmapParameter bevelHeightmapParameter;

        public ID2D1Image Output => output ?? input ?? throw new NullReferenceException();

        bool isFirst = true;
        BevelMode mode;
        double thickness;
        int[] activeSteps = [];
        Vector4 sourceRect;

        ID2D1Image? input;
        readonly BevelHeightmapCustomEffect? fallbackHeightmap;
        readonly BevelSdfSeedCustomEffect? sdfSeed;
        readonly BevelSdfJumpCustomEffect[] sdfJumps = [];
        readonly BevelSdfResolveCustomEffect? sdfResolve;
        readonly ID2D1Image? output;

        public BevelHeightmapSource(IGraphicsDevicesAndContext devices, BevelHeightmapParameter bevelHeightmapParameter)
        {
            this.devices = devices;
            this.bevelHeightmapParameter = bevelHeightmapParameter;

            var seed = new BevelSdfSeedCustomEffect(devices);
            var resolve = new BevelSdfResolveCustomEffect(devices);
            var jumps = Enumerable.Range(0, MaxJumpPasses)
                .Select(_ => new BevelSdfJumpCustomEffect(devices))
                .ToArray();

            if (seed.IsEnabled && resolve.IsEnabled && jumps.All(x => x.IsEnabled))
            {
                sdfSeed = seed;
                sdfResolve = resolve;
                sdfJumps = jumps;
                disposer.Collect(seed);
                disposer.Collect(resolve);
                foreach (var jump in jumps)
                    disposer.Collect(jump);

                output = resolve.Output;
                disposer.Collect(output);
            }
            else
            {
                seed.Dispose();
                resolve.Dispose();
                foreach (var jump in jumps)
                    jump.Dispose();

                var fallback = new BevelHeightmapCustomEffect(devices);
                if (fallback.IsEnabled)
                {
                    fallbackHeightmap = fallback;
                    disposer.Collect(fallback);
                    output = fallback.Output;
                    disposer.Collect(output);
                }
                else
                {
                    fallback.Dispose();
                }
            }
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (output is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var mode = bevelHeightmapParameter.BevelMode;
            var thickness = Math.Clamp(bevelHeightmapParameter.Thickness.GetValue(frame, length, fps), 0, MaxThickness);

            if (fallbackHeightmap is not null)
            {
                if (isFirst || this.thickness != thickness)
                    fallbackHeightmap.Thickness = (float)thickness;
                if (isFirst || this.mode != mode)
                    fallbackHeightmap.Mode = mode;
            }
            else if (sdfSeed is not null && sdfResolve is not null)
            {
                UpdateSourceRect();

                if (isFirst || this.thickness != thickness)
                {
                    sdfResolve.Thickness = (float)thickness;
                    ConfigureSdfChain(CreateJumpSteps(thickness));
                }
                if (isFirst || this.mode != mode)
                    sdfResolve.Mode = mode;
            }

            isFirst = false;
            this.mode = mode;
            this.thickness = thickness;

            return effectDescription.DrawDescription;
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            fallbackHeightmap?.SetInput(0, input, true);
            sdfSeed?.SetInput(0, input, true);
            sdfResolve?.SetInput(1, input, true);
        }

        public void ClearInput()
        {
            fallbackHeightmap?.SetInput(0, null, true);
            sdfSeed?.SetInput(0, null, true);
            sdfResolve?.SetInput(1, null, true);
        }
        public void Dispose()
        {
            ClearInput();
            sdfResolve?.SetInput(0, null, true);
            foreach (var jump in sdfJumps)
                jump.SetInput(0, null, true);
            disposer.Dispose();
        }

        void UpdateSourceRect()
        {
            if (input is null || sdfSeed is null || sdfResolve is null)
                return;

            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            var rect = new Vector4(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            if (!isFirst && sourceRect == rect)
                return;

            sdfSeed.SourceRect = rect;
            sdfResolve.SourceRect = rect;
            foreach (var jump in sdfJumps)
                jump.SourceRect = rect;
            sourceRect = rect;
        }

        void ConfigureSdfChain(int[] steps)
        {
            if (sdfSeed is null || sdfResolve is null || (!isFirst && activeSteps.SequenceEqual(steps)))
                return;

            foreach (var jump in sdfJumps)
                jump.SetInput(0, null, true);

            ID2D1Image current = sdfSeed.Output;
            try
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    var jump = sdfJumps[i];
                    jump.StepSize = steps[i];
                    jump.SetInput(0, current, true);
                    current.Dispose();
                    current = jump.Output;
                }
                sdfResolve.SetInput(0, current, true);
            }
            finally
            {
                current.Dispose();
            }

            activeSteps = steps;
        }

        static int[] CreateJumpSteps(double thickness)
        {
            if (thickness <= 0)
                return [];

            var radius = Math.Min(MaxThickness, (int)Math.Ceiling(thickness) + 2);
            var firstStep = 1;
            while (firstStep <= radius / 2)
                firstStep *= 2;

            var steps = new List<int>();
            for (var step = firstStep; step >= 1; step /= 2)
                steps.Add(step);
            steps.Add(1);
            return [.. steps];
        }
    }
}
