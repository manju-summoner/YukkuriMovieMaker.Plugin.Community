using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputComposite
{
    [VideoEffect(nameof(Texts.OutputCompositeEffectName), [VideoEffectCategories.Composition], ["合成", "composite", "blend", "output", "出力", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class OutputCompositeEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.OutputCompositeEffectName} {TargetIndex}";

        [Display(GroupName = nameof(Texts.OutputCompositeEffectName), Name = nameof(Texts.OutputCompositeTargetIndexName), Description = nameof(Texts.OutputCompositeTargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16)]
        [Range(0, 1024)]
        [DefaultValue(1)]
        public int TargetIndex { get => targetIndex; set => Set(ref targetIndex, value); }
        int targetIndex = 1;

        [Display(GroupName = nameof(Texts.OutputCompositeEffectName), Name = nameof(Texts.OutputCompositeBlendModeName), Description = nameof(Texts.OutputCompositeBlendModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        Blend blendMode = Blend.Normal;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new OutputCompositeEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
