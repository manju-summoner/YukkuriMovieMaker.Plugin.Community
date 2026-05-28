using ICSharpCode.AvalonEdit.Search;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadSearchLocalization : Localization
    {
        public override string MatchCaseText => Texts.SearchMatchCase;
        public override string MatchWholeWordsText => Texts.SearchMatchWholeWords;
        public override string UseRegexText => Texts.SearchUseRegex;
        public override string FindNextText => Texts.SearchFindNext;
        public override string FindPreviousText => Texts.SearchFindPrevious;
        public override string ErrorText => Texts.SearchError;
        public override string NoMatchesFoundText => Texts.SearchNoMatchesFound;
    }
}
