using System;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    /// <summary>
    /// 物理ベースのレンズフレアを生成するカスタムエフェクト。
    /// 入力画像は参照せず、SCENE_POSITION（原点=キャンバス中心）から手続き的に描画する。
    /// 入力にはFlood等を接続し、下流のCropで描画範囲を決める（FractalNoiseと同じ構成）。
    /// </summary>
    sealed class LensFlareCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float LightX
        {
            set => SetValue((int)EffectImpl.Properties.LightX, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightX);
        }
        public float LightY
        {
            set => SetValue((int)EffectImpl.Properties.LightY, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightY);
        }
        public float CanvasWidth
        {
            set => SetValue((int)EffectImpl.Properties.CanvasWidth, value);
            get => GetFloatValue((int)EffectImpl.Properties.CanvasWidth);
        }
        public float CanvasHeight
        {
            set => SetValue((int)EffectImpl.Properties.CanvasHeight, value);
            get => GetFloatValue((int)EffectImpl.Properties.CanvasHeight);
        }
        public float Intensity
        {
            set => SetValue((int)EffectImpl.Properties.Intensity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Intensity);
        }
        public float Scale
        {
            set => SetValue((int)EffectImpl.Properties.Scale, value);
            get => GetFloatValue((int)EffectImpl.Properties.Scale);
        }
        public float Blades
        {
            set => SetValue((int)EffectImpl.Properties.Blades, value);
            get => GetFloatValue((int)EffectImpl.Properties.Blades);
        }
        /// <summary>絞りの回転（ラジアン）</summary>
        public float Rotation
        {
            set => SetValue((int)EffectImpl.Properties.Rotation, value);
            get => GetFloatValue((int)EffectImpl.Properties.Rotation);
        }
        public float GhostCount
        {
            set => SetValue((int)EffectImpl.Properties.GhostCount, value);
            get => GetFloatValue((int)EffectImpl.Properties.GhostCount);
        }
        public float GhostBrightness
        {
            set => SetValue((int)EffectImpl.Properties.GhostBrightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.GhostBrightness);
        }
        public float HaloRadius
        {
            set => SetValue((int)EffectImpl.Properties.HaloRadius, value);
            get => GetFloatValue((int)EffectImpl.Properties.HaloRadius);
        }
        public float HaloBrightness
        {
            set => SetValue((int)EffectImpl.Properties.HaloBrightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.HaloBrightness);
        }
        public float Dispersion
        {
            set => SetValue((int)EffectImpl.Properties.Dispersion, value);
            get => GetFloatValue((int)EffectImpl.Properties.Dispersion);
        }
        public float StarLength
        {
            set => SetValue((int)EffectImpl.Properties.StarLength, value);
            get => GetFloatValue((int)EffectImpl.Properties.StarLength);
        }
        public float StarBrightness
        {
            set => SetValue((int)EffectImpl.Properties.StarBrightness, value);
            get => GetFloatValue((int)EffectImpl.Properties.StarBrightness);
        }
        public float Seed
        {
            set => SetValue((int)EffectImpl.Properties.Seed, value);
            get => GetFloatValue((int)EffectImpl.Properties.Seed);
        }
        public float ColorR
        {
            set => SetValue((int)EffectImpl.Properties.ColorR, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorR);
        }
        public float ColorG
        {
            set => SetValue((int)EffectImpl.Properties.ColorG, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorG);
        }
        public float ColorB
        {
            set => SetValue((int)EffectImpl.Properties.ColorB, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorB);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            public EffectImpl() : base(ShaderResourceUri.Get("LensFlare"))
            {
                constants.Intensity = 1f;
                constants.Scale = 1f;
                constants.Blades = 6f;
                constants.Dispersion = 1f;
                constants.ColorR = 1f;
                constants.ColorG = 1f;
                constants.ColorB = 1f;
                constants.ColorA = 1f;
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightX)]
            public float LightX
            {
                get => constants.LightX;
                set { constants.LightX = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightY)]
            public float LightY
            {
                get => constants.LightY;
                set { constants.LightY = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.CanvasWidth)]
            public float CanvasWidth
            {
                get => constants.CanvasWidth;
                set { constants.CanvasWidth = Math.Max(1f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.CanvasHeight)]
            public float CanvasHeight
            {
                get => constants.CanvasHeight;
                set { constants.CanvasHeight = Math.Max(1f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Intensity)]
            public float Intensity
            {
                get => constants.Intensity;
                set { constants.Intensity = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Scale)]
            public float Scale
            {
                get => constants.Scale;
                set { constants.Scale = Math.Max(0.01f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Blades)]
            public float Blades
            {
                get => constants.Blades;
                set { constants.Blades = Math.Clamp(value, 3f, 32f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Rotation)]
            public float Rotation
            {
                get => constants.Rotation;
                set { constants.Rotation = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.GhostCount)]
            public float GhostCount
            {
                get => constants.GhostCount;
                set { constants.GhostCount = Math.Clamp(value, 0f, 24f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.GhostBrightness)]
            public float GhostBrightness
            {
                get => constants.GhostBrightness;
                set { constants.GhostBrightness = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.HaloRadius)]
            public float HaloRadius
            {
                get => constants.HaloRadius;
                set { constants.HaloRadius = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.HaloBrightness)]
            public float HaloBrightness
            {
                get => constants.HaloBrightness;
                set { constants.HaloBrightness = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Dispersion)]
            public float Dispersion
            {
                get => constants.Dispersion;
                set { constants.Dispersion = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StarLength)]
            public float StarLength
            {
                get => constants.StarLength;
                set { constants.StarLength = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.StarBrightness)]
            public float StarBrightness
            {
                get => constants.StarBrightness;
                set { constants.StarBrightness = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Seed)]
            public float Seed
            {
                get => constants.Seed;
                set { constants.Seed = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorR)]
            public float ColorR
            {
                get => constants.ColorR;
                set { constants.ColorR = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorG)]
            public float ColorG
            {
                get => constants.ColorG;
                set { constants.ColorG = Math.Max(0f, value); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorB)]
            public float ColorB
            {
                get => constants.ColorB;
                set { constants.ColorB = Math.Max(0f, value); UpdateConstants(); }
            }

            protected override void UpdateConstants()
                => drawInformation?.SetPixelShaderConstantBuffer(constants);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float LightX;         // c0.x
                public float LightY;         // c0.y
                public float CanvasWidth;    // c0.z
                public float CanvasHeight;   // c0.w

                public float Intensity;      // c1.x
                public float Scale;          // c1.y
                public float Blades;         // c1.z
                public float Rotation;       // c1.w

                public float GhostCount;     // c2.x
                public float GhostBrightness;// c2.y
                public float HaloRadius;     // c2.z
                public float HaloBrightness; // c2.w

                public float Dispersion;     // c3.x
                public float StarLength;     // c3.y
                public float StarBrightness; // c3.z
                public float Seed;           // c3.w

                public float ColorR;         // c4.x
                public float ColorG;         // c4.y
                public float ColorB;         // c4.z
                public float ColorA;         // c4.w（未使用・アライメント用）
            }

            public enum Properties : int
            {
                LightX = 0,
                LightY = 1,
                CanvasWidth = 2,
                CanvasHeight = 3,
                Intensity = 4,
                Scale = 5,
                Blades = 6,
                Rotation = 7,
                GhostCount = 8,
                GhostBrightness = 9,
                HaloRadius = 10,
                HaloBrightness = 11,
                Dispersion = 12,
                StarLength = 13,
                StarBrightness = 14,
                Seed = 15,
                ColorR = 16,
                ColorG = 17,
                ColorB = 18,
            }
        }
    }
}
