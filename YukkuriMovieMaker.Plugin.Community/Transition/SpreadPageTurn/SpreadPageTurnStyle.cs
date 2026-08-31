using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    //ShowPropertyEditorWhenがHasFlag比較のため、値は1,2,4,…の重複しない単一ビットにする
    //（0や合成ビットを使うと意図しない選択時にも表示されてしまう）
    internal enum SpreadPageTurnStyle
    {
        [Display(Name = nameof(Texts.SpreadPageTurnStyleCurlName), Description = nameof(Texts.SpreadPageTurnStyleCurlName), ResourceType = typeof(Texts))]
        Curl = 1,
        [Display(Name = nameof(Texts.SpreadPageTurnStyleFoldName), Description = nameof(Texts.SpreadPageTurnStyleFoldName), ResourceType = typeof(Texts))]
        Fold = 2,
    }
}
