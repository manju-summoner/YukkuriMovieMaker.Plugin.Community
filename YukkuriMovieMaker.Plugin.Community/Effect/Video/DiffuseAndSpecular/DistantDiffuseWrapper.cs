using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    class DistantDiffuseWrapper(DistantDiffuse effect) : IDiffuseEffectWrapper
    {
        public float Azimuth
        {
            get => effect.Azimuth;
            set => effect.Azimuth = value;
        }
        public float Elevation
        {
            get => effect.Elevation;
            set => effect.Elevation = value;
        }
        public float DiffuseConstant
        {
            get => effect.DiffuseConstant;
            set => effect.DiffuseConstant = value;
        }
        public Vector3 Color
        {
            get => effect.Color;
            set => effect.Color = value;
        }
        public float SurfaceScale
        {
            get => effect.SurfaceScale;
            set => effect.SurfaceScale = value;
        }

        public void SetInput(int index, ID2D1Image? input, bool invalidate) => effect.SetInput(index, input, invalidate);

        public ID2D1Image Output => effect.Output;

        public void Dispose()
        {
            effect.Dispose();
        }

    }
}
