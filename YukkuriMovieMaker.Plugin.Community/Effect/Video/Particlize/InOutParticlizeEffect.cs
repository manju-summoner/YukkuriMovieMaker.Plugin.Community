using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    [VideoEffect(nameof(Texts.InOutParticlize), [VideoEffectCategories.Transition], [nameof(Texts.TagParticle), nameof(Texts.TagParticlize), nameof(Texts.TagDissolve), nameof(Texts.TagDisappear)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class InOutParticlizeEffect : InOutEffectBase
    {
        public override string Label => CreateLabelText(Texts.InParticlizeLabel, Texts.OutParticlizeLabel);

        [Display(GroupName = nameof(Texts.ParticlizeGroup), Name = nameof(Texts.Size), Description = nameof(Texts.SizeDesc), Order = 400, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "px", 1, 50)]
        [Range(1d, 10000d)]
        [DefaultValue(4d)]
        public double Size { get => size; set => Set(ref size, value); }
        double size = 4;

        [Display(GroupName = nameof(Texts.DissolveGroup), Name = nameof(Texts.Angle), Description = nameof(Texts.AngleDesc), Order = 500, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", -180, 180)]
        [Range(-36000d, 36000d)]
        [DefaultValue(0d)]
        public double Angle { get => angle; set => Set(ref angle, value); }
        double angle = 0;

        [Display(GroupName = nameof(Texts.DissolveGroup), Name = nameof(Texts.Randomness), Description = nameof(Texts.RandomnessDesc), Order = 600, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Randomness { get => randomness; set => Set(ref randomness, value); }
        double randomness = 50;

        [Display(GroupName = nameof(Texts.DissolveGroup), Name = nameof(Texts.Lifetime), Description = nameof(Texts.LifetimeDesc), Order = 700, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0.05, 2, ResourceType = typeof(Texts))]
        [Range(0.01d, 10000d)]
        [DefaultValue(0.5d)]
        public double Lifetime { get => lifetime; set => Set(ref lifetime, value); }
        double lifetime = 0.5;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.ScatterAngle), Description = nameof(Texts.ScatterAngleDesc), Order = 800, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", -180, 180)]
        [Range(-36000d, 36000d)]
        [DefaultValue(-90d)]
        public double ScatterAngle { get => scatterAngle; set => Set(ref scatterAngle, value); }
        double scatterAngle = -90;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Speed), Description = nameof(Texts.SpeedDesc), Order = 900, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 10000d)]
        [DefaultValue(50d)]
        public double Speed { get => speed; set => Set(ref speed, value); }
        double speed = 50;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Spread), Description = nameof(Texts.SpreadDesc), Order = 1000, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 10000d)]
        [DefaultValue(50d)]
        public double Spread { get => spread; set => Set(ref spread, value); }
        double spread = 50;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Gravity), Description = nameof(Texts.GravityDesc), Order = 1100, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", -100, 100)]
        [Range(-10000d, 10000d)]
        [DefaultValue(0d)]
        public double Gravity { get => gravity; set => Set(ref gravity, value); }
        double gravity = 0;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Turbulence), Description = nameof(Texts.TurbulenceDesc), Order = 1200, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 10000d)]
        [DefaultValue(50d)]
        public double Turbulence { get => turbulence; set => Set(ref turbulence, value); }
        double turbulence = 50;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDesc), Order = 1300, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 10000d)]
        [DefaultValue(50d)]
        public double Rotation { get => rotation; set => Set(ref rotation, value); }
        double rotation = 50;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Shrink), Description = nameof(Texts.ShrinkDesc), Order = 1400, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(100d)]
        public double Shrink { get => shrink; set => Set(ref shrink, value); }
        double shrink = 100;

        [Display(GroupName = nameof(Texts.ScatterGroup), Name = nameof(Texts.Fade), Description = nameof(Texts.FadeDesc), Order = 1500, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(100d)]
        public double Fade { get => fade; set => Set(ref fade, value); }
        double fade = 100;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new InOutParticlizeEffectProcessor(devices, this);
    }
}
