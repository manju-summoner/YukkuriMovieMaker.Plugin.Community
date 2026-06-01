using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputMapComposite
{
    [VideoEffect(nameof(Texts.OutputMapCompositeEffectName), [VideoEffectCategories.Branch], ["分岐", "branch", "合成", "composite", "blend", "map", "マップ", "lerp"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class OutputMapCompositeEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.OutputMapCompositeEffectName} T:{TargetIndex} M:{MapIndex}";

        [Display(GroupName = nameof(Texts.OutputMapCompositeEffectName), Name = nameof(Texts.OutputMapCompositeTargetIndexName), Description = nameof(Texts.OutputMapCompositeTargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16), Range(0, 1024), DefaultValue(1)]
        public int TargetIndex
        {
            get => _targetIndex;
            set => Set(ref _targetIndex, Math.Max(0, value), nameof(TargetIndex), nameof(Label));
        }
        private int _targetIndex = 1;

        [Display(GroupName = nameof(Texts.OutputMapCompositeEffectName), Name = nameof(Texts.OutputMapCompositeMapIndexName), Description = nameof(Texts.OutputMapCompositeMapIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16), Range(0, 1024), DefaultValue(2)]
        public int MapIndex
        {
            get => _mapIndex;
            set => Set(ref _mapIndex, Math.Max(0, value), nameof(MapIndex), nameof(Label));
        }
        private int _mapIndex = 2;

        [Display(GroupName = nameof(Texts.OutputMapCompositeEffectName), Name = nameof(Texts.OutputMapCompositeMapTypeName), Description = nameof(Texts.OutputMapCompositeMapTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public OutputMapCompositeMapType MapType
        {
            get => _mapType;
            set => Set(ref _mapType, value);
        }
        private OutputMapCompositeMapType _mapType = OutputMapCompositeMapType.Luminance;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new OutputMapCompositeEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
