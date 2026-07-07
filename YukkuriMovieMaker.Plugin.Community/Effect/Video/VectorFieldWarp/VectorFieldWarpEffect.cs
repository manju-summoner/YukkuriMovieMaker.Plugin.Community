using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    [VideoEffect(nameof(Texts.VectorFieldWarpEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagVectorField), nameof(Texts.TagVortex), nameof(Texts.TagAttraction), nameof(Texts.TagWarp)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class VectorFieldWarpEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.VectorFieldWarpEffectName} - {Points.Count}";

        [Display(GroupName = nameof(Texts.VectorFieldWarpEffectName), Name = nameof(Texts.VectorFieldWarpAmountName), Description = nameof(Texts.VectorFieldWarpAmountDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Amount { get; } = new(100, 0, 100);

        [Display(GroupName = nameof(Texts.VectorFieldWarpEffectName), Name = nameof(Texts.VectorFieldWarpMaxDisplacementName), Description = nameof(Texts.VectorFieldWarpMaxDisplacementDesc), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation MaxDisplacement { get; } = new(200, 0, VectorFieldWarpCustomEffect.MaxDisplacementLimit);

        [Display(GroupName = nameof(Texts.VectorFieldWarpEffectName), Name = nameof(Texts.VectorFieldWarpIntegrationStepsName), Description = nameof(Texts.VectorFieldWarpIntegrationStepsDesc), Order = 2, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps)]
        [Range(1, VectorFieldWarpCustomEffect.MaxIntegrationSteps)]
        [DefaultValue(8)]
        public int IntegrationSteps { get => integrationSteps; set => Set(ref integrationSteps, Math.Clamp(value, 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps)); }
        int integrationSteps = 8;

        [Display(GroupName = nameof(Texts.VectorFieldWarpEffectName), Description = nameof(Texts.VectorFieldWarpPointsDesc), Order = 10, ResourceType = typeof(Texts))]
        [VectorFieldPointListEditor]
        public ImmutableList<VectorFieldPoint> Points
        {
            get => points;
            set
            {
                if (Set(ref points, value ?? ImmutableList<VectorFieldPoint>.Empty))
                    OnPropertyChanged(nameof(Label));
            }
        }
        ImmutableList<VectorFieldPoint> points = [VectorFieldPoint.Create(0, 0)];

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new VectorFieldWarpEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            yield return Amount;
            yield return MaxDisplacement;
            foreach (var point in Points)
            {
                yield return point.X;
                yield return point.Y;
                yield return point.RadialStrength;
                yield return point.VortexStrength;
                yield return point.Radius;
            }
        }
    }
}
