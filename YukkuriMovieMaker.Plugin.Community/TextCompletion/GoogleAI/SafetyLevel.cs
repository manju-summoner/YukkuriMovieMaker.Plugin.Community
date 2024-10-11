using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    public enum SafetyLevel
    {
        [Display(Name = nameof(Texts.BlockNone), ResourceType = typeof(Texts))]
        BlockNone,
        [Display(Name = nameof(Texts.BlockFew), ResourceType = typeof(Texts))]
        BlockFew,
        [Display(Name = nameof(Texts.BlockSome), ResourceType = typeof(Texts))]
        BlockSome,
        [Display(Name = nameof(Texts.BlockMost), ResourceType = typeof(Texts))]
        BlockMost,
    }
}
