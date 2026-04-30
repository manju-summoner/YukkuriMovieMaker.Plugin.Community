using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GodRay
{
    [VideoEffect(nameof(Texts.GodRayEffectName), [VideoEffectCategories.Drawing], ["godray", "god ray", "light ray", "sun ray", "光線", "ゴッドレイ"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class GodRayEffect : VideoEffectBase
    {
        public override string Label => Texts.GodRayEffectName;

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayLightXName), Description = nameof(Texts.GodRayLightXDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation LightX { get; } = new Animation(50, 0d, 100d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayLightYName), Description = nameof(Texts.GodRayLightYDesc), Order = 101, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation LightY { get; } = new Animation(20, 0d, 100d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayIntensityName), Description = nameof(Texts.GodRayIntensityDesc), Order = 102, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 2d)]
        public Animation Intensity { get; } = new Animation(1.0, 0d, 10d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayDecayName), Description = nameof(Texts.GodRayDecayDesc), Order = 103, ResourceType = typeof(Texts))]
        [AnimationSlider("F3", "", 0.9d, 1d)]
        public Animation Decay { get; } = new Animation(0.90, 0d, 1d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayDensityName), Description = nameof(Texts.GodRayDensityDesc), Order = 104, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0.1d, 2d)]
        public Animation Density { get; } = new Animation(0.8, 0d, 5d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayWeightName), Description = nameof(Texts.GodRayWeightDesc), Order = 105, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation Weight { get; } = new Animation(0.2, 0d, 1d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRaySamplesName), Description = nameof(Texts.GodRaySamplesDesc), Order = 106, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 8d, 128d)]
        public Animation Samples { get; } = new Animation(128, 1d, 256d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayThresholdName), Description = nameof(Texts.GodRayThresholdDesc), Order = 107, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation Threshold { get; } = new Animation(0.1, 0d, 1d);

        [Display(GroupName = nameof(Texts.GodRayEffectName), Name = nameof(Texts.GodRayColorName), Description = nameof(Texts.GodRayColorDesc), Order = 108, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color LightColor { get => lightColor; set => Set(ref lightColor, value); }
        Color lightColor = Color.FromArgb(255, 255, 240, 180);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new GodRayEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            LightX,
            LightY,
            Intensity,
            Decay,
            Density,
            Weight,
            Samples,
            Threshold,
        ];
    }
}
