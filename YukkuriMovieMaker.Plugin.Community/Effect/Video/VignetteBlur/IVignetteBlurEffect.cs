using System.Numerics;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VignetteBlur
{
    internal interface IVignetteBlurEffect : IDisposable
    {
        Vector2 Center { get; set; }
        float Radius { get; set; }
        float Aspect { get; set; }
        float Softness { get; set; }
        bool IsFixedSize { get; set; }
        float Blur { get; set; }
        float Lightness { get; set; }
        float ColorShift { get; set; }

        int InputCount { get; }
        bool IsEnabled { get; }
        ID2D1Image Output { get; }
        void SetInput(int index, ID2D1Image? input, SharpGen.Runtime.RawBool invalidate);
    }
}