using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using static YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading.ParameterNormalizer;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    internal sealed class CrossHatchShadingEffectProcessor(IGraphicsDevicesAndContext devices, CrossHatchShadingEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly CrossHatchShadingEffect item = item;
        CrossHatchShadingCustomEffect? effect;
        Parameters parameters;
        bool isFirst = true;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var next = new Parameters(
                Percent(item.Amount.GetValue(frame, length, fps), 0f, 1f, 1f),
                Finite(item.Density.GetValue(frame, length, fps), 3f, 320f, 12f),
                Finite(item.Thickness.GetValue(frame, length, fps), 0.2f, 48f, 1.2f),
                Percent(item.ShadowDepth.GetValue(frame, length, fps), 0f, 2f, 1f),
                Percent(item.Flow.GetValue(frame, length, fps), 0f, 1f, 0.4f),
                Percent(item.Wobble.GetValue(frame, length, fps), 0f, 1f, 0.25f),
                Percent(item.Outline.GetValue(frame, length, fps), 0f, 1f, 0.3f),
                Percent(item.ToneSoftness.GetValue(frame, length, fps), 0f, 0.5f, 0.12f),
                Percent(item.Grain.GetValue(frame, length, fps), 0f, 1f, 0.12f),
                Math.Clamp(item.Seed, 0, 9999),
                (int)item.Style,
                (int)item.ColorMode,
                (int)item.LineLayers,
                ToVector(item.InkColor),
                ToVector(item.PaperColor));

            if (isFirst || parameters != next)
            {
                effect.Amount = next.Amount;
                effect.Density = next.Density;
                effect.Thickness = next.Thickness;
                effect.ShadowDepth = next.ShadowDepth;
                effect.Flow = next.Flow;
                effect.Wobble = next.Wobble;
                effect.Outline = next.Outline;
                effect.ToneSoftness = next.ToneSoftness;
                effect.Grain = next.Grain;
                effect.Seed = next.Seed;
                effect.Style = next.Style;
                effect.ColorMode = next.ColorMode;
                effect.LineLayers = next.LineLayers;
                effect.InkColor = next.InkColor;
                effect.PaperColor = next.PaperColor;
                parameters = next;
                isFirst = false;
            }

            return effectDescription.DrawDescription;
        }

        static Vector4 ToVector(System.Windows.Media.Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new CrossHatchShadingCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }

            disposer.Collect(effect);
            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            isFirst = true;
        }

        readonly record struct Parameters(
            float Amount,
            float Density,
            float Thickness,
            float ShadowDepth,
            float Flow,
            float Wobble,
            float Outline,
            float ToneSoftness,
            float Grain,
            int Seed,
            int Style,
            int ColorMode,
            int LineLayers,
            Vector4 InkColor,
            Vector4 PaperColor);
    }
}
