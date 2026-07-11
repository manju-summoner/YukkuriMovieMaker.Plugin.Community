using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    internal sealed class InOutParticlizeEffectProcessor(IGraphicsDevicesAndContext devices, InOutParticlizeEffect item) : InOutEffectBase<InOutParticlizeEffect>(devices, item)
    {
        ParticlizeRenderer? renderer;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || renderer is null)
                return effectDescription.DrawDescription;

            //効果時間内に粒子化が完了するよう、伝播期間は効果時間から寿命を引いた残りにする
            var lifetime = Math.Max(0.01, item.Lifetime);
            var dissolveSpan = Math.Max(0, item.EffectTimeSeconds - lifetime);

            //イージング値は効果区間の端で0（完全粒子化）、区間内で1（未粒子化）。
            //1から引いた進行度を粒子化の経過時間へ割り当てる（登場時は時間が巻き戻り、粒子が集まって出現する）
            var progress = 1 - GetEasingValue(effectDescription, 0, 1);
            var time = TimeSpan.FromSeconds(progress * (dissolveSpan + lifetime));

            var parameter = new ParticlizeRenderer.Parameter(
                item.Size,
                dissolveSpan,
                item.Angle,
                item.Randomness,
                lifetime,
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
