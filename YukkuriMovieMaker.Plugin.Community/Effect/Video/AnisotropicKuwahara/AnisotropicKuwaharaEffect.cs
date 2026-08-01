using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AnisotropicKuwahara
{
    [VideoEffect(nameof(Texts.AnisotropicKuwaharaEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagPainterly), nameof(Texts.TagKuwahara), nameof(Texts.TagOilPainting), nameof(Texts.TagWatercolor)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class AnisotropicKuwaharaEffect : VideoEffectBase
    {
        public override string Label => Texts.AnisotropicKuwaharaEffectName;

        [Display(GroupName = nameof(Texts.AnisotropicKuwaharaEffectName), Name = nameof(Texts.AnisotropicKuwaharaRadiusName), Description = nameof(Texts.AnisotropicKuwaharaRadiusDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 1d, 40d)]
        public Animation Radius { get; } = new Animation(8, 1, 40);

        [Display(GroupName = nameof(Texts.AnisotropicKuwaharaEffectName), Name = nameof(Texts.AnisotropicKuwaharaSharpnessName), Description = nameof(Texts.AnisotropicKuwaharaSharpnessDesc), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", 1d, 20d)]
        public Animation Sharpness { get; } = new Animation(8, 1, 20);

        [Display(GroupName = nameof(Texts.AnisotropicKuwaharaEffectName), Name = nameof(Texts.AnisotropicKuwaharaAnisotropyName), Description = nameof(Texts.AnisotropicKuwaharaAnisotropyDesc), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Anisotropy { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.AnisotropicKuwaharaEffectName), Name = nameof(Texts.AnisotropicKuwaharaQualityName), Description = nameof(Texts.AnisotropicKuwaharaQualityDesc), Order = 3, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public AnisotropicKuwaharaQuality Quality
        {
            get => _quality;
            set => Set(ref _quality, value);
        }
        private AnisotropicKuwaharaQuality _quality = AnisotropicKuwaharaQuality.High;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new AnisotropicKuwaharaEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [Radius, Sharpness, Anisotropy];
    }
}
