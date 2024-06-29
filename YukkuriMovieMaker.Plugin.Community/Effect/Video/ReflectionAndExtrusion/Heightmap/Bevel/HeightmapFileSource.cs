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
        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly BevelHeightmapParameter bevelHeightmapParameter;

        public ID2D1Image Output => output ?? input ?? throw new NullReferenceException();

        bool isFirst = true;
        BevelMode mode;
        double thickness;

        ID2D1Image? input;
        readonly BevelHeightmapCustomEffect? heightmap;
        readonly ID2D1Image? output;

        public BevelHeightmapSource(IGraphicsDevicesAndContext devices, BevelHeightmapParameter bevelHeightmapParameter)
        {
            this.devices = devices;
            this.bevelHeightmapParameter = bevelHeightmapParameter;

            heightmap = new(devices);
            if(heightmap.IsEnabled)
            {
                disposer.Collect(heightmap);
                output = heightmap.Output;
                disposer.Collect(output);
            }
            else
            {
                heightmap.Dispose();
                heightmap = null;
            }
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (output is null || heightmap is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var mode = bevelHeightmapParameter.BevelMode;
            var thickness = bevelHeightmapParameter.Thickness.GetValue(frame, length, fps);

            if (isFirst || this.thickness != thickness)
                heightmap.Thickness = (float)thickness;
            if(isFirst || this.mode != mode)
                heightmap.Mode = mode;

            isFirst = false;
            this.mode = mode;
            this.thickness = thickness;

            return effectDescription.DrawDescription;
        }

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            heightmap?.SetInput(0, input, true);
        }

        public void ClearInput()
        {
            heightmap?.SetInput(0, null, true);
        }
        public void Dispose()
        {
            heightmap?.SetInput(0, null, true);
            disposer.Dispose();
        }
    }
}