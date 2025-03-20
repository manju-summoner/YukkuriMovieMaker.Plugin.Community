using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.InOutCrop
{
    public enum CropDirection
    {
        [Display(Name = nameof(Texts.InOutCropEffectCropDirectionTopName), Description = nameof(Texts.InOutCropEffectCropDirectionTopDesc), ResourceType = typeof(Texts))]
        Top,
        [Display(Name = nameof(Texts.InOutCropEffectCropDirectionBottomName), Description = nameof(Texts.InOutCropEffectCropDirectionBottomDesc), ResourceType = typeof(Texts))]
        Bottom,
        [Display(Name = nameof(Texts.InOutCropEffectCropDirectionLeftName), Description = nameof(Texts.InOutCropEffectCropDirectionLeftDesc), ResourceType = typeof(Texts))]
        Left,
        [Display(Name = nameof(Texts.InOutCropEffectCropDirectionRightName), Description = nameof(Texts.InOutCropEffectCropDirectionRightDesc), ResourceType = typeof(Texts))]
        Right,
        [Display(Name = nameof(Texts.InOutCropEffectCropDirectionNoneName), Description = nameof(Texts.InOutCropEffectCropDirectionNoneDesc), ResourceType = typeof(Texts))]
        None,
    }
}