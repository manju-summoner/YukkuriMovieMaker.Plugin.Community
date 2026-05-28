using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    [VideoEffect(nameof(Texts.OutputBranchEffectName), [VideoEffectCategories.Composition], ["分岐", "branch", "出力", "output" ], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class OutputBranchEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.OutputBranchEffectName} (+{BranchCount})";

        [Display(GroupName = nameof(Texts.OutputBranchEffectName), Name = nameof(Texts.OutputBranchCountName), Description = nameof(Texts.OutputBranchCountDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1, 4)]
        [Range(1, 64)]
        [DefaultValue(1)]
        public int BranchCount { get => branchCount; set => Set(ref branchCount, value, nameof(BranchCount), nameof(Label)); }
        int branchCount = 1;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new OutputBranchEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
