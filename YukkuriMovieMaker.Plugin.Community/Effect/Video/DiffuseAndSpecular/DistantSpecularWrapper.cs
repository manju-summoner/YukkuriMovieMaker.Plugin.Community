using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    class DistantSpecularWrapper(DistantSpecular effect) : ISpecularEffectWrapper
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
        public float SpecularConstant
        {
            get => effect.SpecularConstant;
            set => effect.SpecularConstant = value;
        }
        public float SpecularExponent
        {
            get => effect.SpecularExponent;
            set => effect.SpecularExponent = value;
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
