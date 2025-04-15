using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Shape.NumberText;

public enum TextAlignment
{
    [Display(Name = nameof(Texts.BasePointLeftTop), ResourceType = typeof(Texts))]
    LeftTop,

    [Display(Name = nameof(Texts.BasePointCenterTop), ResourceType = typeof(Texts))]
    CenterTop,

    [Display(Name = nameof(Texts.BasePointRightTop), ResourceType = typeof(Texts))]
    RightTop,

    [Display(Name = nameof(Texts.BasePointLeftCenter), ResourceType = typeof(Texts))]
    LeftCenter,

    [Display(Name = nameof(Texts.BasePointCenterCenter), ResourceType = typeof(Texts))]
    CenterCenter,

    [Display(Name = nameof(Texts.BasePointRightCenter), ResourceType = typeof(Texts))]
    RightCenter,

    [Display(Name = nameof(Texts.BasePointLeftBottom), ResourceType = typeof(Texts))]
    LeftBottom,

    [Display(Name = nameof(Texts.BasePointCenterBottom), ResourceType = typeof(Texts))]
    CenterBottom,

    [Display(Name = nameof(Texts.BasePointRightBottom), ResourceType = typeof(Texts))]
    RightBottom
}
