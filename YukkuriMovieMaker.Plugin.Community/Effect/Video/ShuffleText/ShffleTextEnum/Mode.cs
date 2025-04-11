using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleText.ShffleTextEnum
{
    public enum Mode
    {
        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_Mode_Alphabet), Description = nameof(Texts.ShuffleTextEffectEnum_Mode_Discription_Alphabet), ResourceType = typeof(Texts))]
        Alphabet = 1,

        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_Mode_Number), Description = nameof(Texts.ShuffleTextEffectEnum_Mode_Discription_Number), ResourceType = typeof(Texts))]
        Number = 2,

        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_Mode_Symbol), Description = nameof(Texts.ShuffleTextEffectEnum_Mode_Discription_Symbol), ResourceType = typeof(Texts))]
        Symbol = 3,

        [Display(Name = nameof(Texts.ShuffleTextEffectEnum_Mode_Custom), Description = nameof(Texts.ShuffleTextEffectEnum_Mode_Discription_Custom), ResourceType = typeof(Texts))]
        Custom = 4,
    }
}
