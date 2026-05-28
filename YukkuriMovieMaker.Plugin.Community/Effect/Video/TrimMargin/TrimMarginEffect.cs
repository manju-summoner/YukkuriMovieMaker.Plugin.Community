using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TrimMargin
{
    [VideoEffect(nameof(Texts.TrimMarginEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagTrimMargin), nameof(Texts.TagCrop), nameof(Texts.TagTransparent)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class TrimMarginEffect : VideoEffectBase
    {
        public override string Label => Texts.TrimMarginEffectName;

        [Display(GroupName = nameof(Texts.TrimMarginEffectName), Name = nameof(Texts.TrimMarginCenterName), Description = nameof(Texts.TrimMarginCenterDesc), Order = 0, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Center
        {
            get => _center;
            set => Set(ref _center, value);
        }
        private bool _center = true;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new TrimMarginEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
