using YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size.Parameter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size
{
    internal static class SizeModeEx
    {
        public static SizeParameterBase Convert(this SizeMode mode, SizeParameterBase current)
        {
            var store = current.GetSharedData();
            SizeParameterBase param = mode switch
            {
                SizeMode.BothStretch => new BothStretchParameter(store),
                SizeMode.WidthStretch => new WidthStretchParameter(store),
                SizeMode.HeightStretch => new HeightStretchParameter(store),
                SizeMode.BothFit => new BothFitParameter(store),
                SizeMode.BothFill => new BothFillParameter(store),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            if (param.GetType() != current.GetType())
                return param;
            return current;
        }
    }
}
