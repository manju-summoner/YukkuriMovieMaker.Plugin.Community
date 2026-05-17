using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Growl
{
    [AudioEffect(nameof(Texts.GrowlEffect), [AudioEffectCategories.Effect], ["growl", "distortion", "がなり", "歪み"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class GrowlEffect : AudioEffectBase
    {
        public override string Label => $"{Texts.GrowlEffect} {DriveDb.GetValue(0, 1, 30):F1}dB";

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.DriveDbName), Description = nameof(Texts.DriveDbDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", 0, 36)]
        public Animation DriveDb { get; } = new Animation(10, 0, 60);

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.AsymmetryName), Description = nameof(Texts.AsymmetryDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -1, 1)]
        public Animation Asymmetry { get; } = new Animation(0.35, -1, 1);

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.RoughnessName), Description = nameof(Texts.RoughnessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Roughness { get; } = new Animation(45, 0, 100);

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.RoughnessFreqName), Description = nameof(Texts.RoughnessFreqDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "Hz", 20, 120)]
        public Animation RoughnessFreq { get; } = new Animation(65, 10, 200);

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.ToneDbName), Description = nameof(Texts.ToneDbDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation ToneDb { get; } = new Animation(2, -24, 24);

        [Display(GroupName = nameof(Texts.GrowlEffect), Name = nameof(Texts.MixName), Description = nameof(Texts.MixDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Mix { get; } = new Animation(100, 0, 100);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new GrowlEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [DriveDb, Asymmetry, Roughness, RoughnessFreq, ToneDb, Mix];
    }
}
