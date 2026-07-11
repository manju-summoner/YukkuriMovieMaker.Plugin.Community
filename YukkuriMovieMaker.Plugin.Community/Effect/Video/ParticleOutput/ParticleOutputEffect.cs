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
        [TextBoxSlider("F1", nameof(Texts.PerSecUnit), 0.1, 100, ResourceType = typeof(Texts))]
        [Range(0.1d, 2000d)]
        [DefaultValue(10d)]
        public double Rate { get => rate; set => Set(ref rate, value); }
        double rate = 10;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Lifetime), Description = nameof(Texts.LifetimeDesc), Order = 200, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0.1, 5, ResourceType = typeof(Texts))]
        [Range(0.01d, 20d)]
        [DefaultValue(2d)]
        public double Lifetime { get => lifetime; set => Set(ref lifetime, value); }
        double lifetime = 2;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Preroll), Description = nameof(Texts.PrerollDesc), Order = 300, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0, 10, ResourceType = typeof(Texts))]
        [Range(0d, 100d)]
        [DefaultValue(0d)]
        public double Preroll { get => preroll; set => Set(ref preroll, value); }
        double preroll = 0;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Size), Description = nameof(Texts.SizeDesc), Order = 400, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 1, 100)]
        [Range(0.1d, 1000d)]
        [DefaultValue(100d)]
        public double Size { get => size; set => Set(ref size, value); }
        double size = 100;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.EmitRange), Description = nameof(Texts.EmitRangeDesc), Order = 500, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "px", 0, 500)]
        [Range(0d, 10000d)]
        [DefaultValue(0d)]
        public double EmitRange { get => emitRange; set => Set(ref emitRange, value); }
        double emitRange = 0;

        [Display(GroupName = nameof(Texts.EmitGroup), Name = nameof(Texts.Randomness), Description = nameof(Texts.RandomnessDesc), Order = 600, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Randomness { get => randomness; set => Set(ref randomness, value); }
        double randomness = 50;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.EmitAngle), Description = nameof(Texts.EmitAngleDesc), Order = 700, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", -180, 180)]
        [Range(-36000d, 36000d)]
        [DefaultValue(-90d)]
        public double EmitAngle { get => emitAngle; set => Set(ref emitAngle, value); }
        double emitAngle = -90;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.SpreadAngle), Description = nameof(Texts.SpreadAngleDesc), Order = 800, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", 0, 360)]
        [Range(0d, 360d)]
        [DefaultValue(60d)]
        public double SpreadAngle { get => spreadAngle; set => Set(ref spreadAngle, value); }
        double spreadAngle = 60;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Speed), Description = nameof(Texts.SpeedDesc), Order = 900, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "px/s", 0, 1000)]
        [Range(0d, 10000d)]
        [DefaultValue(400d)]
        public double Speed { get => speed; set => Set(ref speed, value); }
        double speed = 400;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Gravity), Description = nameof(Texts.GravityDesc), Order = 1000, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "px/s", -500, 500)]
        [Range(-10000d, 10000d)]
        [DefaultValue(0d)]
        public double Gravity { get => gravity; set => Set(ref gravity, value); }
        double gravity = 0;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.WindAngle), Description = nameof(Texts.WindAngleDesc), Order = 1100, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", -180, 180)]
        [Range(-36000d, 36000d)]
        [DefaultValue(0d)]
        public double WindAngle { get => windAngle; set => Set(ref windAngle, value); }
        double windAngle = 0;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.WindSpeed), Description = nameof(Texts.WindSpeedDesc), Order = 1200, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "px/s", 0, 500)]
        [Range(0d, 10000d)]
        [DefaultValue(0d)]
        public double WindSpeed { get => windSpeed; set => Set(ref windSpeed, value); }
        double windSpeed = 0;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Turbulence), Description = nameof(Texts.TurbulenceDesc), Order = 1300, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 10000d)]
        [DefaultValue(30d)]
        public double Turbulence { get => turbulence; set => Set(ref turbulence, value); }
        double turbulence = 30;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDesc), Order = 1400, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°/s", 0, 360)]
        [Range(0d, 36000d)]
        [DefaultValue(90d)]
        public double Rotation { get => rotation; set => Set(ref rotation, value); }
        double rotation = 90;

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.EndScale), Description = nameof(Texts.EndScaleDesc), Order = 1500, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 200)]
        [Range(0d, 1000d)]
        [DefaultValue(100d)]
        public double EndScale { get => endScale; set => Set(ref endScale, value); }
        double endScale = 100;

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Fade), Description = nameof(Texts.FadeDesc), Order = 1600, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(100d)]
        public double Fade { get => fade; set => Set(ref fade, value); }
        double fade = 100;

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.ShowOriginal), Description = nameof(Texts.ShowOriginalDesc), Order = 1700, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool ShowOriginal { get => showOriginal; set => Set(ref showOriginal, value); }
        bool showOriginal = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new ParticleOutputEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
