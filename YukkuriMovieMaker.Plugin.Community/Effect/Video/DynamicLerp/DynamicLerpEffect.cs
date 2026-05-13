using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DynamicLerp
{
    [VideoEffect(nameof(Texts.DynamicLerpEffectName), [VideoEffectCategories.Composition], ["lerp", "blend", "map", "dynamic lerp", "ダイナミックラープ", "補間", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class DynamicLerpEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.DynamicLerpEffectName} T:{TargetIndex} M:{MapIndex}";

        [Display(GroupName = nameof(Texts.DynamicLerpEffectName), Name = nameof(Texts.TargetIndexName), Description = nameof(Texts.TargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16), Range(0, 1024), DefaultValue(1)]
        public int TargetIndex
        {
            get => _targetIndex;
            set => Set(ref _targetIndex, Math.Max(0, value), nameof(TargetIndex), nameof(Label));
        }
        private int _targetIndex = 1;

        [Display(GroupName = nameof(Texts.DynamicLerpEffectName), Name = nameof(Texts.MapIndexName), Description = nameof(Texts.MapIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16), Range(0, 1024), DefaultValue(2)]
        public int MapIndex
        {
            get => _mapIndex;
            set => Set(ref _mapIndex, Math.Max(0, value), nameof(MapIndex), nameof(Label));
        }
        private int _mapIndex = 2;

        [Display(GroupName = nameof(Texts.DynamicLerpEffectName), Name = nameof(Texts.MapTypeName), Description = nameof(Texts.MapTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DynamicLerpMapType MapType
        {
            get => _mapType;
            set => Set(ref _mapType, value);
        }
        private DynamicLerpMapType _mapType = DynamicLerpMapType.Luminance;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new DynamicLerpEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
