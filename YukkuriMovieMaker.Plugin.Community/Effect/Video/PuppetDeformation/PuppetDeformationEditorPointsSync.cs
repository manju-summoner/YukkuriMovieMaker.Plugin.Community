using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public enum PuppetDeformationEditorPointsSync
    {
        [Display(Name = nameof(Texts.PuppetDeformationEditorSyncModeNoneName), Description = nameof(Texts.PuppetDeformationEditorSyncModeNoneDesc), ResourceType = typeof(Texts))]
        None,
        [Display(Name = nameof(Texts.PuppetDeformationEditorSyncModeDistanceName), Description = nameof(Texts.PuppetDeformationEditorSyncModeDistanceDesc), ResourceType = typeof(Texts))]
        Distance,
        [Display(Name = nameof(Texts.PuppetDeformationEditorSyncModeParallelName), Description = nameof(Texts.PuppetDeformationEditorSyncModeParallelDesc), ResourceType = typeof(Texts))]
        Parallel
    }
}
