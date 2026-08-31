using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    internal enum SpreadPageTurnPage
    {
        [Display(Name = nameof(Texts.SpreadPageTurnPageRightName), Description = nameof(Texts.SpreadPageTurnPageRightName), ResourceType = typeof(Texts))]
        Right = 0,
        [Display(Name = nameof(Texts.SpreadPageTurnPageLeftName), Description = nameof(Texts.SpreadPageTurnPageLeftName), ResourceType = typeof(Texts))]
        Left = 1,
        [Display(Name = nameof(Texts.SpreadPageTurnPageBottomName), Description = nameof(Texts.SpreadPageTurnPageBottomName), ResourceType = typeof(Texts))]
        Bottom = 2,
        [Display(Name = nameof(Texts.SpreadPageTurnPageTopName), Description = nameof(Texts.SpreadPageTurnPageTopName), ResourceType = typeof(Texts))]
        Top = 3,
    }
}
