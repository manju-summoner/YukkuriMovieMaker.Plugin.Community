using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.DynamicEffect
{
    [AudioEffect(nameof(Texts.ConditionalDynamicsEffect), [AudioEffectCategories.Effect], ["dynamics", "conditional", "gate", "ダイナミクス", "条件分岐"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class ConditionalDynamicsEffect : AudioEffectBase
    {
        public override string Label => $"{Texts.ConditionalDynamicsEffect} {ThresholdDb.GetValue(0, 1, 30):0.0}dB";

        [Display(GroupName = nameof(Texts.DetectionGroup), Name = nameof(Texts.DetectionModeName), Description = nameof(Texts.DetectionModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DetectionMode DetectionMode { get => detectionMode; set => Set(ref detectionMode, value); }
        DetectionMode detectionMode = DetectionMode.Rms;

        [Display(GroupName = nameof(Texts.DetectionGroup), Name = nameof(Texts.RmsWindowMsName), Description = nameof(Texts.RmsWindowMsDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "ms", 1, 200)]
        [DefaultValue(10d)]
        [Range(1, 200)]
        [RmsWindowVisible]
        public double RmsWindowMs { get => rmsWindowMs; set => Set(ref rmsWindowMs, value); }
        double rmsWindowMs = 10;

        [Display(GroupName = nameof(Texts.DetectionGroup), Name = nameof(Texts.ThresholdDbName), Description = nameof(Texts.ThresholdDbDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -60, 0)]
        public Animation ThresholdDb { get; } = new Animation(-20, -60, 0);

        [Display(GroupName = nameof(Texts.DetectionGroup), Name = nameof(Texts.AttackMsName), Description = nameof(Texts.AttackMsDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "ms", 1, 200)]
        [DefaultValue(10d)]
        [Range(1, 200)]
        public double AttackMs { get => attackMs; set => Set(ref attackMs, value); }
        double attackMs = 10;

        [Display(GroupName = nameof(Texts.DetectionGroup), Name = nameof(Texts.ReleaseMsName), Description = nameof(Texts.ReleaseMsDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "ms", 1, 500)]
        [DefaultValue(100d)]
        [Range(1, 500)]
        public double ReleaseMs { get => releaseMs; set => Set(ref releaseMs, value); }
        double releaseMs = 100;

        [Display(GroupName = nameof(Texts.BelowEffectsGroup), Description = nameof(Texts.BelowEffectsDesc), ResourceType = typeof(Texts))]
        [AudioEffectSelector]
        public ImmutableList<IAudioEffect> BelowEffects { get => belowEffects; set => Set(ref belowEffects, value); }
        ImmutableList<IAudioEffect> belowEffects = [];

        [Display(GroupName = nameof(Texts.AboveEffectsGroup), Description = nameof(Texts.AboveEffectsDesc), ResourceType = typeof(Texts))]
        [AudioEffectSelector]
        public ImmutableList<IAudioEffect> AboveEffects { get => aboveEffects; set => Set(ref aboveEffects, value); }
        ImmutableList<IAudioEffect> aboveEffects = [];

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new ConditionalDynamicsEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [ThresholdDb];
    }
}
