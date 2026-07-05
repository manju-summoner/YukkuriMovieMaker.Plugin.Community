using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.MotionBlur
{
    [VideoEffect(nameof(Texts.MotionBlurEffectName), [VideoEffectCategories.Filtering], ["motion blur", "モーションブラー", "残像"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class MotionBlurEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.MotionBlurEffectName} {Amount.GetValue(0, 1, 30):F0}%";

        [Display(GroupName = nameof(Texts.MotionBlurEffectName), Name = nameof(Texts.MotionBlurEffectAmountName), Description = nameof(Texts.MotionBlurEffectAmountDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Amount { get; } = new Animation(50, 0, 1600);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new MotionBlurEffectProcessor(devices, this);
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Amount];
    }
}
