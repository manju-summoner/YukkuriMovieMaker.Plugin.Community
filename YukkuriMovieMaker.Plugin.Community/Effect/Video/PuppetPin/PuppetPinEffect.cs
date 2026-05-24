using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetPin
{
    [VideoEffect(nameof(Texts.PuppetPinEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagPuppet), nameof(Texts.TagPin), nameof(Texts.TagWarp), nameof(Texts.TagDeform)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class PuppetPinEffect : VideoEffectBase
    {
        public const int PinCapacity = PuppetPinCustomEffect.MaxPins;

        public override string Label => $"{Texts.PuppetPinEffectName} ({Pins.Count})";

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinStiffnessName), Description = nameof(Texts.PuppetPinStiffnessDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0.5d, 4d)]
        public Animation Stiffness { get; } = new Animation(2.0, 0.1, 8.0);

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Name = nameof(Texts.PuppetPinEditorSyncModeName), Description = nameof(Texts.PuppetPinEditorSyncModeDesc), Order = 5, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PuppetPinEditorPointsSync SyncMode
        {
            get => syncMode;
            set => Set(ref syncMode, value);
        }
        PuppetPinEditorPointsSync syncMode = PuppetPinEditorPointsSync.Distance;

        [Display(GroupName = nameof(Texts.PuppetPinEffectName), Description = nameof(Texts.PuppetPinListPinsDesc), Order = 10, ResourceType = typeof(Texts))]
        [PuppetPinListEditor]
        public ImmutableList<PuppetPin> Pins
        {
            get => pins;
            set
            {
                if (Set(ref pins, value))
                    OnPropertyChanged(nameof(Label));
            }
        }
        ImmutableList<PuppetPin> pins = ImmutableList<PuppetPin>.Empty;

        public PuppetPinEffect()
        {
            Pins = ImmutableList.Create(
                PuppetPin.Create(-100, -100),
                PuppetPin.Create(100, -100),
                PuppetPin.Create(-100, 100),
                PuppetPin.Create(100, 100)
            );
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new PuppetPinEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            yield return Stiffness;
            foreach (var pin in Pins)
            {
                yield return pin.RestX;
                yield return pin.RestY;
                yield return pin.OffsetX;
                yield return pin.OffsetY;
            }
        }
    }
}
