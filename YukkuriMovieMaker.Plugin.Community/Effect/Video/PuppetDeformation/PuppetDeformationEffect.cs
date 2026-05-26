using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    [VideoEffect(nameof(Texts.PuppetDeformationEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagPuppet), nameof(Texts.TagPin), nameof(Texts.TagWarp), nameof(Texts.TagDeform)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class PuppetDeformationEffect : VideoEffectBase
    {
        public const int PinCapacity = PuppetDeformationCustomEffect.MaxPins;

        public override string Label => $"{Texts.PuppetDeformationEffectName} ({Pins.Count})";

        [Display(GroupName = nameof(Texts.PuppetDeformationEffectName), Name = nameof(Texts.PuppetDeformationStiffnessName), Description = nameof(Texts.PuppetDeformationStiffnessDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0.5d, 4d)]
        public Animation Stiffness { get; } = new Animation(2.0, 0.1, 8.0);

        [Display(GroupName = nameof(Texts.PuppetDeformationEffectName), Name = nameof(Texts.PuppetDeformationEditorSyncModeName), Description = nameof(Texts.PuppetDeformationEditorSyncModeDesc), Order = 5, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PuppetDeformationEditorPointsSync SyncMode
        {
            get => syncMode;
            set => Set(ref syncMode, value);
        }
        PuppetDeformationEditorPointsSync syncMode = PuppetDeformationEditorPointsSync.Distance;

        [Display(GroupName = nameof(Texts.PuppetDeformationEffectName), Description = nameof(Texts.PuppetDeformationListPinsDesc), Order = 10, ResourceType = typeof(Texts))]
        [PuppetDeformationListEditor]
        public ImmutableList<PuppetDeformation> Pins
        {
            get => pins;
            set
            {
                if (Set(ref pins, value ?? ImmutableList<PuppetDeformation>.Empty))
                    OnPropertyChanged(nameof(Label));
            }
        }
        ImmutableList<PuppetDeformation> pins = ImmutableList<PuppetDeformation>.Empty;

        public PuppetDeformationEffect()
        {
            Pins = ImmutableList.Create(
                PuppetDeformation.Create(-100, -100),
                PuppetDeformation.Create(100, -100),
                PuppetDeformation.Create(-100, 100),
                PuppetDeformation.Create(100, 100)
            );
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new PuppetDeformationEffectProcessor(devices, this);

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
