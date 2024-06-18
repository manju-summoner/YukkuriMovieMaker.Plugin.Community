using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    interface IEffectWrapper : IDisposable
    {
        void SetInput(int index, ID2D1Image? input, bool invalidate);
        ID2D1Image Output { get; }
    }
}
