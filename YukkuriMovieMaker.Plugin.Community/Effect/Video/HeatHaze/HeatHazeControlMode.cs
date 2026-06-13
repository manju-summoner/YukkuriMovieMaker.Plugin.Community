using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    public enum HeatHazeControlMode
    {
        [Display(Name = nameof(Texts.HeatHazeControlModeAutomatic), Description = nameof(Texts.HeatHazeControlModeAutomaticDesc), ResourceType = typeof(Texts))]
        Automatic,

        [Display(Name = nameof(Texts.HeatHazeControlModeManual), Description = nameof(Texts.HeatHazeControlModeManualDesc), ResourceType = typeof(Texts))]
        Manual,
    }
}
