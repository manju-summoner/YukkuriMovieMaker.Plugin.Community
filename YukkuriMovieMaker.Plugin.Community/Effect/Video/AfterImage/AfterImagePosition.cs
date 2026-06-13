using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AfterImage
{
    public enum AfterImagePosition
    {
        [Display(Name = nameof(Texts.AfterImagePositionFront), ResourceType = typeof(Texts))]
        Front = 0,
        [Display(Name = nameof(Texts.AfterImagePositionBack), ResourceType = typeof(Texts))]
        Back = 1,
        [Display(Name = nameof(Texts.AfterImagePositionAfterImageOnly), ResourceType = typeof(Texts))]
        AfterImageOnly = 2,
    }
}
