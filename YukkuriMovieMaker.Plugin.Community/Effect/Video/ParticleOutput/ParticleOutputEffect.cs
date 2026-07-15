using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    [VideoEffect(nameof(Texts.ParticleOutput), [VideoEffectCategories.Decoration], [nameof(Texts.TagParticle), nameof(Texts.TagSparkle), nameof(Texts.TagSnow), nameof(Texts.TagEmitter)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class ParticleOutputEffect : VideoEffectBase
    {
        public override string Label => Texts.ParticleOutput;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Rate), Description = nameof(Texts.RateDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.PerSecUnit), 0, 100, ResourceType = typeof(Texts))]
        public Animation Rate { get; } = new Animation(10, 0, 2000);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Lifetime), Description = nameof(Texts.LifetimeDesc), Order = 200, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), 0.1, 5, ResourceType = typeof(Texts))]
        public Animation Lifetime { get; } = new Animation(2, 0.01, 20);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Preroll), Description = nameof(Texts.PrerollDesc), Order = 300, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0, 10, ResourceType = typeof(Texts))]
        [Range(0d, 100d)]
        [DefaultValue(0d)]
        public double Preroll { get => preroll; set => Set(ref preroll, value); }
        double preroll = 0;

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Size), Description = nameof(Texts.SizeDesc), Order = 800, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 1, 100)]
        public Animation Size { get; } = new Animation(100, 0.1, 1000);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.EmitX), Description = nameof(Texts.EmitXDesc), Order = 400, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.EmitY), Description = nameof(Texts.EmitYDesc), Order = 500, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.EmitZ), Description = nameof(Texts.EmitZDesc), Order = 600, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.EmitRange), Description = nameof(Texts.EmitRangeDesc), Order = 700, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation EmitRange { get; } = new Animation(0, 0, 10000);

        [Display(GroupName = nameof(Texts.RandomGroup), Name = nameof(Texts.Randomness), Description = nameof(Texts.RandomnessDesc), Order = 1700, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Randomness { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.LaunchGroup), Name = nameof(Texts.EmitAngle), Description = nameof(Texts.EmitAngleDesc), Order = 1200, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation EmitAngle { get; } = new Animation(-90, -36000, 36000);

        [Display(GroupName = nameof(Texts.LaunchGroup), Name = nameof(Texts.EmitElevation), Description = nameof(Texts.EmitElevationDesc), Order = 1300, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -90, 90)]
        public Animation EmitElevation { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.RandomGroup), Name = nameof(Texts.SpreadAngle), Description = nameof(Texts.SpreadAngleDesc), Order = 1500, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation SpreadAngle { get; } = new Animation(60, 0, 360);

        [Display(GroupName = nameof(Texts.RandomGroup), Name = nameof(Texts.ElevationSpreadAngle), Description = nameof(Texts.ElevationSpreadAngleDesc), Order = 1600, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 180)]
        public Animation ElevationSpreadAngle { get; } = new Animation(0, 0, 180);

        [Display(GroupName = nameof(Texts.LaunchGroup), Name = nameof(Texts.Speed), Description = nameof(Texts.SpeedDesc), Order = 1400, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px/s", 0, 1000)]
        public Animation Speed { get; } = new Animation(400, 0, 10000);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Perspective), Description = nameof(Texts.PerspectiveDesc), Order = 1100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Perspective { get; } = new Animation(100, 0, 1000);

        [Display(GroupName = nameof(Texts.ForceFieldGroup), Name = nameof(Texts.Gravity), Description = nameof(Texts.GravityDesc), Order = 2000, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px/s", -500, 500)]
        public Animation Gravity { get; } = new Animation(0, -10000, 10000);

        [Display(GroupName = nameof(Texts.ForceFieldGroup), Name = nameof(Texts.WindAngle), Description = nameof(Texts.WindAngleDesc), Order = 2100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation WindAngle { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.ForceFieldGroup), Name = nameof(Texts.WindSpeed), Description = nameof(Texts.WindSpeedDesc), Order = 2200, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px/s", 0, 500)]
        public Animation WindSpeed { get; } = new Animation(0, 0, 10000);

        [Display(GroupName = nameof(Texts.RandomGroup), Name = nameof(Texts.Turbulence), Description = nameof(Texts.TurbulenceDesc), Order = 1900, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Turbulence { get; } = new Animation(30, 0, 10000);

        [Display(GroupName = nameof(Texts.CurlTurbulenceGroup), Name = nameof(Texts.CurlStrength), Description = nameof(Texts.CurlStrengthDesc), Order = 2300, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 200)]
        public Animation CurlStrength { get; } = new Animation(0, 0, 10000);

        [Display(GroupName = nameof(Texts.CurlTurbulenceGroup), Name = nameof(Texts.CurlScale), Description = nameof(Texts.CurlScaleDesc), Order = 2400, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 10, 500)]
        public Animation CurlScale { get; } = new Animation(100, 1, 100000);

        [Display(GroupName = nameof(Texts.CurlTurbulenceGroup), Name = nameof(Texts.CurlSpeed), Description = nameof(Texts.CurlSpeedDesc), Order = 2500, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -200, 200)]
        public Animation CurlSpeed { get; } = new Animation(100, -1000, 1000);

        [Display(GroupName = nameof(Texts.RandomGroup), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDesc), Order = 1800, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°/s", 0, 360)]
        public Animation Rotation { get; } = new Animation(90, 0, 36000);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.EndScale), Description = nameof(Texts.EndScaleDesc), Order = 900, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation EndScale { get; } = new Animation(100, 0, 1000);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Fade), Description = nameof(Texts.FadeDesc), Order = 1000, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Fade { get; } = new Animation(100, 0, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new ParticleOutputEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
            [Rate, Lifetime, X, Y, Z, Size, EmitRange, Randomness, EmitAngle, EmitElevation, SpreadAngle, ElevationSpreadAngle, Speed, Perspective, Gravity, WindAngle, WindSpeed, Turbulence, CurlStrength, CurlScale, CurlSpeed, Rotation, EndScale, Fade];
    }
}
