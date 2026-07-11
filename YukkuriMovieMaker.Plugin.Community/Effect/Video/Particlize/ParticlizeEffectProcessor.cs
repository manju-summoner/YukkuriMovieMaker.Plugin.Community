using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    internal sealed class ParticlizeEffectProcessor(IGraphicsDevicesAndContext devices, ParticlizeEffect item) : VideoEffectProcessorBase(devices)
    {
        ParticlizeRenderer? renderer;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || renderer is null)
                return effectDescription.DrawDescription;

            var playbackRate = item.PlaybackRate / 100;
            var startTime = TimeSpan.FromSeconds(item.StartTime);
            var time = playbackRate < 0
                ? effectDescription.ItemPosition.Time * playbackRate + startTime
                : effectDescription.ItemPosition.Time * playbackRate - startTime;

            var parameter = new ParticlizeRenderer.Parameter(
                item.Size,
                item.DissolveTime,
                item.Angle,
                item.Randomness,
                item.Lifetime,
                item.ScatterAngle,
                item.Speed,
                item.Spread,
                item.WindAngle,
                item.WindSpeed,
                item.Gravity,
                item.Turbulence,
                item.Rotation,
                item.Shrink,
                item.Fade,
                item.GetHashCode());

            renderer.Update(effectDescription, input, time, parameter);
            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            renderer = new ParticlizeRenderer(devices, disposer);
            return renderer.CreateEffect();
        }

        protected override void setInput(ID2D1Image? input)
        {
            renderer?.SetInput(input);
        }

        protected override void ClearEffectChain()
        {
            renderer?.ClearEffectChain();
        }
    }
}
