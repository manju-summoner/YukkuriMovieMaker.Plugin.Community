using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    internal enum PageTurnOrigin
    {
        [Display(Name = nameof(Texts.PageTurnOriginBottomRightName), Description = nameof(Texts.PageTurnOriginBottomRightName), ResourceType = typeof(Texts))]
        BottomRight = 0,
        [Display(Name = nameof(Texts.PageTurnOriginBottomLeftName), Description = nameof(Texts.PageTurnOriginBottomLeftName), ResourceType = typeof(Texts))]
        BottomLeft = 1,
        [Display(Name = nameof(Texts.PageTurnOriginTopLeftName), Description = nameof(Texts.PageTurnOriginTopLeftName), ResourceType = typeof(Texts))]
        TopLeft = 2,
        [Display(Name = nameof(Texts.PageTurnOriginTopRightName), Description = nameof(Texts.PageTurnOriginTopRightName), ResourceType = typeof(Texts))]
        TopRight = 3,
        [Display(Name = nameof(Texts.PageTurnOriginRightName), Description = nameof(Texts.PageTurnOriginRightName), ResourceType = typeof(Texts))]
        Right = 4,
        [Display(Name = nameof(Texts.PageTurnOriginLeftName), Description = nameof(Texts.PageTurnOriginLeftName), ResourceType = typeof(Texts))]
        Left = 5,
        [Display(Name = nameof(Texts.PageTurnOriginTopName), Description = nameof(Texts.PageTurnOriginTopName), ResourceType = typeof(Texts))]
        Top = 6,
        [Display(Name = nameof(Texts.PageTurnOriginBottomName), Description = nameof(Texts.PageTurnOriginBottomName), ResourceType = typeof(Texts))]
        Bottom = 7,
    }
}
