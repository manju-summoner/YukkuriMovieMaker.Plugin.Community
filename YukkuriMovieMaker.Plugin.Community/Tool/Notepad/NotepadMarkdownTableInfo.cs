using System.Collections.Generic;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownTableInfo
    {
        public int FirstLine { get; init; }
        public int LastLine { get; init; }
        public int SeparatorLine { get; init; }
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
        public IReadOnlyList<NotepadMarkdownTableAlignment> Alignments { get; init; } = [];
        public int ColumnCount { get; init; }

        public bool ContainsLine(int lineNumber) => lineNumber >= FirstLine && lineNumber <= LastLine;
        public bool IsHeaderLine(int lineNumber) => lineNumber == FirstLine;
    }
}
