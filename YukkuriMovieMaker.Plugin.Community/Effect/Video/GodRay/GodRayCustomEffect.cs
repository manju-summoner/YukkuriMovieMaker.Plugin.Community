using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GodRay
{
    internal class GodRayCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
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
        public float Intensity
        {
            set => SetValue((int)EffectImpl.Properties.Intensity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Intensity);
        }
        public float Decay
        {
            set => SetValue((int)EffectImpl.Properties.Decay, value);
            get => GetFloatValue((int)EffectImpl.Properties.Decay);
        }
        public float Density
        {
            set => SetValue((int)EffectImpl.Properties.Density, value);
            get => GetFloatValue((int)EffectImpl.Properties.Density);
        }
        public float Weight
        {
            set => SetValue((int)EffectImpl.Properties.Weight, value);
            get => GetFloatValue((int)EffectImpl.Properties.Weight);
        }
        public float Samples
        {
            set => SetValue((int)EffectImpl.Properties.Samples, value);
            get => GetFloatValue((int)EffectImpl.Properties.Samples);
        }
        public float Threshold
        {
            set => SetValue((int)EffectImpl.Properties.Threshold, value);
            get => GetFloatValue((int)EffectImpl.Properties.Threshold);
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
        public float ColorA
        {
            set => SetValue((int)EffectImpl.Properties.ColorA, value);
            get => GetFloatValue((int)EffectImpl.Properties.ColorA);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

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
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Intensity)]
            public float Intensity
            {
                get => constants.Intensity;
                set { constants.Intensity = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Decay)]
            public float Decay
            {
                get => constants.Decay;
                set { constants.Decay = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Density)]
            public float Density
            {
                get => constants.Density;
                set { constants.Density = Math.Max(value, 0f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Weight)]
            public float Weight
            {
                get => constants.Weight;
                set { constants.Weight = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Samples)]
            public float Samples
            {
                get => constants.Samples;
                set { constants.Samples = Math.Max(value, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Threshold)]
            public float Threshold
            {
                get => constants.Threshold;
                set { constants.Threshold = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorR)]
            public float ColorR
            {
                get => constants.ColorR;
                set { constants.ColorR = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorG)]
            public float ColorG
            {
                get => constants.ColorG;
                set { constants.ColorG = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorB)]
            public float ColorB
            {
                get => constants.ColorB;
                set { constants.ColorB = value; UpdateConstants(); }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.ColorA)]
            public float ColorA
            {
                get => constants.ColorA;
                set { constants.ColorA = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("GodRay"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                var density = constants.Density;
                var lightAbs = new Vector2(constants.LightX, constants.LightY);

                // 出力ピクセル P が i 番目のサンプルで inputRect に到達する条件:
                //   sampleAbs(i) = P*(1-τ) + lightAbs*τ ∈ inputRect,  τ = i*density/N ∈ (0, density]
                // P について解くと P(τ) = lightAbs + (C - lightAbs)/(1-τ),  C ∈ inputRect。
                // density < 1 では P は直線上を C から pMax(C) = (C - density*lightAbs)/(1-density) まで動き、
                // 全 C・全 τ の和集合の axis-aligned bounding box は inputRect の4コーナーと
                // pMax(コーナー) の合計8点の bounding box で厳密に決まる。
                // density >= 1 では (C - density*lightAbs)/(1-density) が発散するため理論上無限遠まで広がる。
                const int MaxOutputExpand = 4096;
                float minX = inputRect.Left - MaxOutputExpand;
                float minY = inputRect.Top - MaxOutputExpand;
                float maxX = inputRect.Right + MaxOutputExpand;
                float maxY = inputRect.Bottom + MaxOutputExpand;

                if (density < 1f)
                {
                    float exactMinX = inputRect.Left;
                    float exactMinY = inputRect.Top;
                    float exactMaxX = inputRect.Right;
                    float exactMaxY = inputRect.Bottom;
                    var corners = new[]
                    {
                        new Vector2(inputRect.Left, inputRect.Top),
                        new Vector2(inputRect.Right, inputRect.Top),
                        new Vector2(inputRect.Left, inputRect.Bottom),
                        new Vector2(inputRect.Right, inputRect.Bottom),
                    };
                    foreach (var c in corners)
                    {
                        var pMax = (c - density * lightAbs) / (1 - density);
                        exactMinX = Math.Min(exactMinX, pMax.X);
                        exactMinY = Math.Min(exactMinY, pMax.Y);
                        exactMaxX = Math.Max(exactMaxX, pMax.X);
                        exactMaxY = Math.Max(exactMaxY, pMax.Y);
                    }
                    // 厳密境界を採用するが、density が 1 に近い場合の暴発を避けるため絶対上限でクランプ
                    minX = Math.Max(minX, exactMinX);
                    minY = Math.Max(minY, exactMinY);
                    maxX = Math.Min(maxX, exactMaxX);
                    maxY = Math.Min(maxY, exactMaxY);
                }

                outputRect = new RawRect(
                    (int)Math.Floor(minX),
                    (int)Math.Floor(minY),
                    (int)Math.Ceiling(maxX),
                    (int)Math.Ceiling(maxY)
                );
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var density = constants.Density;
                var lightAbs = new Vector2(constants.LightX, constants.LightY);

                var corners = new[]
                {
                    new Vector2(outputRect.Left, outputRect.Top),
                    new Vector2(outputRect.Right, outputRect.Top),
                    new Vector2(outputRect.Left, outputRect.Bottom),
                    new Vector2(outputRect.Right, outputRect.Bottom),
                };

                float minX = outputRect.Left;
                float minY = outputRect.Top;
                float maxX = outputRect.Right;
                float maxY = outputRect.Bottom;
                foreach (var c in corners)
                {
                    var end = c * (1 - density) + lightAbs * density;
                    minX = Math.Min(minX, end.X);
                    minY = Math.Min(minY, end.Y);
                    maxX = Math.Max(maxX, end.X);
                    maxY = Math.Max(maxY, end.Y);
                }

                // バイリニアサンプリングによる端1pxの参照に備えて1pxマージンを追加
                inputRects[0] = new RawRect(
                    (int)Math.Floor(minX) - 1,
                    (int)Math.Floor(minY) - 1,
                    (int)Math.Ceiling(maxX) + 1,
                    (int)Math.Ceiling(maxY) + 1
                );
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float LightX;
                public float LightY;
                public float Intensity;
                public float Decay;
                public float Density;
                public float Weight;
                public float Samples;
                public float Threshold;
                public float ColorR;
                public float ColorG;
                public float ColorB;
                public float ColorA;
            }

            public enum Properties : int
            {
                LightX = 0,
                LightY = 1,
                Intensity = 2,
                Decay = 3,
                Density = 4,
                Weight = 5,
                Samples = 6,
                Threshold = 7,
                ColorR = 8,
                ColorG = 9,
                ColorB = 10,
                ColorA = 11,
            }
        }
    }
}
