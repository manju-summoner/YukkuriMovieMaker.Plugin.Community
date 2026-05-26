using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public enum PuppetPinEditorPointsSync
    {
        [Display(Name = nameof(Texts.PuppetPinEditorSyncModeNoneName), Description = nameof(Texts.PuppetPinEditorSyncModeNoneDesc), ResourceType = typeof(Texts))]
        None,
        [Display(Name = nameof(Texts.PuppetPinEditorSyncModeDistanceName), Description = nameof(Texts.PuppetPinEditorSyncModeDistanceDesc), ResourceType = typeof(Texts))]
        Distance,
        [Display(Name = nameof(Texts.PuppetPinEditorSyncModeParallelName), Description = nameof(Texts.PuppetPinEditorSyncModeParallelDesc), ResourceType = typeof(Texts))]
        Parallel
    }
}
