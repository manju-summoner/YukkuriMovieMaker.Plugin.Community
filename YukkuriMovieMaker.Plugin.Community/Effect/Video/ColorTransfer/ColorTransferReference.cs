using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    public enum ColorTransferReference
    {
        [Display(Name = nameof(Texts.ColorTransferReferenceScene), Description = nameof(Texts.ColorTransferReferenceSceneDescription), ResourceType = typeof(Texts))]
        Scene = 1,

        [Display(Name = nameof(Texts.ColorTransferReferenceBranch), Description = nameof(Texts.ColorTransferReferenceBranchDescription), ResourceType = typeof(Texts))]
        Branch = 2,
    }
}
