using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSameground
{
    [Flags]
    public enum FillSamegroundMode
    {
        [Display(Name = "位置指定")]
        Position = 1,

        [Display(Name = "色指定")]
        Color = 2,

        [Display(Name = "指定した位置の色指定")]
        PositionColor = 4,
    }
}
