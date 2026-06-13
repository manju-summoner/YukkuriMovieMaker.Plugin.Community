using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSameground
{
    public enum FillSamegroundMode
    {
        [Display(Name = nameof(Texts.FillSamegroundModePositionName), ResourceType = typeof(Texts))]
        Position = 1,

        [Display(Name = nameof(Texts.FillSamegroundModeColorName), ResourceType = typeof(Texts))]
        Color = 2,

        [Display(Name = nameof(Texts.FillSamegroundModePositionColorName), ResourceType = typeof(Texts))]
        PositionColor = 4,
    }
}
