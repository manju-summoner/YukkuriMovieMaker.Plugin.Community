using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputSwitch
{
    [VideoEffect(nameof(Texts.OutputSwitchEffectName), [VideoEffectCategories.Composition], ["切替", "switch", "output", "出力", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class OutputSwitchEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.OutputSwitchEffectName} {TargetIndex}";

        [Display(GroupName = nameof(Texts.OutputSwitchEffectName), Name = nameof(Texts.OutputSwitchTargetIndexName), Description = nameof(Texts.OutputSwitchTargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16)]
        [Range(0, 1024)]
        [DefaultValue(1)]
        public int TargetIndex { get => targetIndex; set => Set(ref targetIndex, value, nameof(TargetIndex), nameof(Label)); }
        int targetIndex = 1;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new OutputSwitchEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
