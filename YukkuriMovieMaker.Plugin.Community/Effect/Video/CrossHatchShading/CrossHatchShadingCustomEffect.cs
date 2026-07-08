using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    internal sealed class CrossHatchShadingCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Amount { get => GetFloatValue((int)EffectImpl.Properties.Amount); set => SetValue((int)EffectImpl.Properties.Amount, value); }
        public float Density { get => GetFloatValue((int)EffectImpl.Properties.Density); set => SetValue((int)EffectImpl.Properties.Density, value); }
        public float Thickness { get => GetFloatValue((int)EffectImpl.Properties.Thickness); set => SetValue((int)EffectImpl.Properties.Thickness, value); }
        public float ShadowDepth { get => GetFloatValue((int)EffectImpl.Properties.ShadowDepth); set => SetValue((int)EffectImpl.Properties.ShadowDepth, value); }
        public float Flow { get => GetFloatValue((int)EffectImpl.Properties.Flow); set => SetValue((int)EffectImpl.Properties.Flow, value); }
        public float Wobble { get => GetFloatValue((int)EffectImpl.Properties.Wobble); set => SetValue((int)EffectImpl.Properties.Wobble, value); }
        public float Outline { get => GetFloatValue((int)EffectImpl.Properties.Outline); set => SetValue((int)EffectImpl.Properties.Outline, value); }
        public float ToneSoftness { get => GetFloatValue((int)EffectImpl.Properties.ToneSoftness); set => SetValue((int)EffectImpl.Properties.ToneSoftness, value); }
        public float Grain { get => GetFloatValue((int)EffectImpl.Properties.Grain); set => SetValue((int)EffectImpl.Properties.Grain, value); }
        public int Seed { get => GetIntValue((int)EffectImpl.Properties.Seed); set => SetValue((int)EffectImpl.Properties.Seed, value); }
        public int Style { get => GetIntValue((int)EffectImpl.Properties.Style); set => SetValue((int)EffectImpl.Properties.Style, value); }
        public int ColorMode { get => GetIntValue((int)EffectImpl.Properties.ColorMode); set => SetValue((int)EffectImpl.Properties.ColorMode, value); }
        public int LineLayers { get => GetIntValue((int)EffectImpl.Properties.LineLayers); set => SetValue((int)EffectImpl.Properties.LineLayers, value); }
        public Vector4 InkColor { get => GetVector4Value((int)EffectImpl.Properties.InkColor); set => SetValue((int)EffectImpl.Properties.InkColor, value); }
        public Vector4 PaperColor { get => GetVector4Value((int)EffectImpl.Properties.PaperColor); set => SetValue((int)EffectImpl.Properties.PaperColor, value); }

        [CustomEffect(1)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const int SampleMargin = 2;

            ConstantBuffer constants = new()
            {
                Amount = 1f,
                Density = 12f,
                Thickness = 1.2f,
                ShadowDepth = 1f,
                Flow = 0.4f,
                Outline = 0.3f,
                ToneSoftness = 0.12f,
                Grain = 0.12f,
                InkColor = new Vector4(17f / 255f, 17f / 255f, 17f / 255f, 1f),
                PaperColor = new Vector4(246f / 255f, 241f / 255f, 231f / 255f, 1f),
                Wobble = 0.25f,
            };

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Amount)]
            public float Amount { get => constants.Amount; set { constants.Amount = Clamp(value, 0f, 1f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Density)]
            public float Density { get => constants.Density; set { constants.Density = Clamp(value, 3f, 320f, 12f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Thickness)]
            public float Thickness { get => constants.Thickness; set { constants.Thickness = Clamp(value, 0.2f, 48f, 1.2f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ShadowDepth)]
            public float ShadowDepth { get => constants.ShadowDepth; set { constants.ShadowDepth = Clamp(value, 0f, 2f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Flow)]
            public float Flow { get => constants.Flow; set { constants.Flow = Clamp(value, 0f, 1f, 0.4f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Wobble)]
            public float Wobble { get => constants.Wobble; set { constants.Wobble = Clamp(value, 0f, 1f, 0.25f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Outline)]
            public float Outline { get => constants.Outline; set { constants.Outline = Clamp(value, 0f, 1f, 0.3f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.ToneSoftness)]
            public float ToneSoftness { get => constants.ToneSoftness; set { constants.ToneSoftness = Clamp(value, 0f, 0.5f, 0.12f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Grain)]
            public float Grain { get => constants.Grain; set { constants.Grain = Clamp(value, 0f, 1f, 0.12f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Seed)]
            public int Seed { get => constants.Seed; set { constants.Seed = Math.Clamp(value, 0, 9999); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Style)]
            public int Style { get => constants.Style; set { constants.Style = Math.Clamp(value, 0, 3); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.ColorMode)]
            public int ColorMode { get => constants.ColorMode; set { constants.ColorMode = Math.Clamp(value, 0, 1); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.LineLayers)]
            public int LineLayers { get => constants.LineLayers; set { constants.LineLayers = Math.Clamp(value, 0, 4); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.InkColor)]
            public Vector4 InkColor { get => constants.InkColor; set { constants.InkColor = Saturate(value); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.PaperColor)]
            public Vector4 PaperColor { get => constants.PaperColor; set { constants.PaperColor = Saturate(value); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("CrossHatchShading"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = inputRects.Length > 0 ? ClampInputRect(inputRects[0]) : default;
                constants.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
                UpdateConstants();
                outputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length == 0)
                    return;

                inputRects[0] = new RawRect(
                    Saturate((long)outputRect.Left - SampleMargin),
                    Saturate((long)outputRect.Top - SampleMargin),
                    Saturate((long)outputRect.Right + SampleMargin),
                    Saturate((long)outputRect.Bottom + SampleMargin));
            }

            static float Clamp(float value, float minimum, float maximum, float fallback)
            {
                if (!float.IsFinite(value))
                    return fallback;
                return Math.Clamp(value, minimum, maximum);
            }

            static Vector4 Saturate(Vector4 value) => Vector4.Clamp(value, Vector4.Zero, Vector4.One);

            static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Amount;
                public float Density;
                public float Thickness;
                public float ShadowDepth;
                public float Flow;
                public float Outline;
                public float ToneSoftness;
                public float Grain;
                public int Seed;
                public int Style;
                public int ColorMode;
                public int LineLayers;
                public Vector4 InkColor;
                public Vector4 PaperColor;
                public Vector4 InputBounds;
                public float Wobble;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }

            public enum Properties
            {
                Amount,
                Density,
                Thickness,
                ShadowDepth,
                Flow,
                Wobble,
                Outline,
                ToneSoftness,
                Grain,
                Seed,
                Style,
                ColorMode,
                LineLayers,
                InkColor,
                PaperColor,
            }
        }
    }
}
