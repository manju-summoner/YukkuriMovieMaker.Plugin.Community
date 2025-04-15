using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleTextInOut.ShffleTextInOut_Enum
{
    public enum DisplayMode
    {
        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_DisplayMode_Simultaneous), Description = nameof(Texts.ShuffleTextEffectEnum_DisplayMode_Discription_Simultaneous), ResourceType = typeof(Texts))]
        Nomal = 1,

        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_DisplayMode_Order), Description = nameof(Texts.ShuffleTextEffectEnum_DisplayMode_Discription_Order), ResourceType = typeof(Texts))]
        Order = 2,
    }
}
