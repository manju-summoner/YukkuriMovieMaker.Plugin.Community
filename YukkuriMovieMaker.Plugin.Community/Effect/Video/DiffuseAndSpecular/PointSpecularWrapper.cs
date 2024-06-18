﻿using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    class PointSpecularWrapper(PointSpecular effect) : ISpecularEffectWrapper
    {
        public Vector3 LightPosition
        {
            get => effect.LightPosition;
            set => effect.LightPosition = value;
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
