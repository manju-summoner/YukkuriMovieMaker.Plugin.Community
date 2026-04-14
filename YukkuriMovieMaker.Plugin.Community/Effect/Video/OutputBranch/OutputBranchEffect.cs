using System.Collections.Generic;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    [VideoEffect(nameof(Texts.OutputBranchEffectName), [VideoEffectCategories.Composition], ["分岐", "branch", "output", "出力", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class OutputBranchEffect : VideoEffectBase
    {
        public override string Label => Texts.OutputBranchEffectName;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new OutputBranchEffectProcessor(devices);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
