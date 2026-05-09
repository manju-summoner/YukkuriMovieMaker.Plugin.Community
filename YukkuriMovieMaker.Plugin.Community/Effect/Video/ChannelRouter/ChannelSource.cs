using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ChannelRouter
{
    public enum ChannelSource
    {
        [Display(Name = nameof(Texts.ChannelSourceCurrentR), Description = nameof(Texts.ChannelSourceCurrentRDesc), ResourceType = typeof(Texts))]
        CurrentR = 0,

        [Display(Name = nameof(Texts.ChannelSourceCurrentG), Description = nameof(Texts.ChannelSourceCurrentGDesc), ResourceType = typeof(Texts))]
        CurrentG = 1,

        [Display(Name = nameof(Texts.ChannelSourceCurrentB), Description = nameof(Texts.ChannelSourceCurrentBDesc), ResourceType = typeof(Texts))]
        CurrentB = 2,

        [Display(Name = nameof(Texts.ChannelSourceCurrentA), Description = nameof(Texts.ChannelSourceCurrentADesc), ResourceType = typeof(Texts))]
        CurrentA = 3,

        [Display(Name = nameof(Texts.ChannelSourceCurrentLuminance), Description = nameof(Texts.ChannelSourceCurrentLuminanceDesc), ResourceType = typeof(Texts))]
        CurrentLuminance = 4,

        [Display(Name = nameof(Texts.ChannelSourceBranchR), Description = nameof(Texts.ChannelSourceBranchRDesc), ResourceType = typeof(Texts))]
        BranchR = 5,

        [Display(Name = nameof(Texts.ChannelSourceBranchG), Description = nameof(Texts.ChannelSourceBranchGDesc), ResourceType = typeof(Texts))]
        BranchG = 6,

        [Display(Name = nameof(Texts.ChannelSourceBranchB), Description = nameof(Texts.ChannelSourceBranchBDesc), ResourceType = typeof(Texts))]
        BranchB = 7,

        [Display(Name = nameof(Texts.ChannelSourceBranchA), Description = nameof(Texts.ChannelSourceBranchADesc), ResourceType = typeof(Texts))]
        BranchA = 8,

        [Display(Name = nameof(Texts.ChannelSourceBranchLuminance), Description = nameof(Texts.ChannelSourceBranchLuminanceDesc), ResourceType = typeof(Texts))]
        BranchLuminance = 9,

        [Display(Name = nameof(Texts.ChannelSourceOne), Description = nameof(Texts.ChannelSourceOneDesc), ResourceType = typeof(Texts))]
        One = 10,

        [Display(Name = nameof(Texts.ChannelSourceZero), Description = nameof(Texts.ChannelSourceZeroDesc), ResourceType = typeof(Texts))]
        Zero = 11,
    }
}
