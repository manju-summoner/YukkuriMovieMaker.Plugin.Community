using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Dithering
{
    public sealed class DitheringCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        private enum PropertyIndex
        {
            Levels = 0,
            Scale,
            Strength,
            Mode,
            DarkR,
            DarkG,
            DarkB,
            LightR,
            LightG,
            LightB,
        }

        public float Levels { set => SetValue((int)PropertyIndex.Levels, value); }
        public float Scale { set => SetValue((int)PropertyIndex.Scale, value); }
        public float Strength { set => SetValue((int)PropertyIndex.Strength, value); }
        public int Mode { set => SetValue((int)PropertyIndex.Mode, value); }
        public float DarkR { set => SetValue((int)PropertyIndex.DarkR, value); }
        public float DarkG { set => SetValue((int)PropertyIndex.DarkG, value); }
        public float DarkB { set => SetValue((int)PropertyIndex.DarkB, value); }
        public float LightR { set => SetValue((int)PropertyIndex.LightR, value); }
        public float LightG { set => SetValue((int)PropertyIndex.LightG, value); }
        public float LightB { set => SetValue((int)PropertyIndex.LightB, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Levels)]
            public float Levels { get => _cb.Levels; set { _cb.Levels = MathF.Round(Math.Clamp(value, 2f, 256f)); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Scale)]
            public float Scale { get => _cb.Scale; set { _cb.Scale = Math.Clamp(value, 1f, 4096f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Strength)]
            public float Strength { get => _cb.Strength; set { _cb.Strength = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)PropertyIndex.Mode)]
            public int Mode { get => _cb.Mode; set { _cb.Mode = Math.Clamp(value, 0, 2); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.DarkR)]
            public float DarkR { get => _cb.DarkColor.X; set { _cb.DarkColor.X = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.DarkG)]
            public float DarkG { get => _cb.DarkColor.Y; set { _cb.DarkColor.Y = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.DarkB)]
            public float DarkB { get => _cb.DarkColor.Z; set { _cb.DarkColor.Z = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightR)]
            public float LightR { get => _cb.LightColor.X; set { _cb.LightColor.X = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightG)]
            public float LightG { get => _cb.LightColor.Y; set { _cb.LightColor.Y = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.LightB)]
            public float LightB { get => _cb.LightColor.Z; set { _cb.LightColor.Z = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("Dithering")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Levels;
                public float Scale;
                public float Strength;
                public int Mode;
                public Vector4 DarkColor;
                public Vector4 LightColor;
            }
        }
    }
}
