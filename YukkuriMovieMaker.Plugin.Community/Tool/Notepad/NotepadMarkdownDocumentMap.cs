using System.Collections.Generic;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownDocumentMap
    {
        private readonly Dictionary<int, NotepadMarkdownBlockInfo> _lineInfos = new();
        private readonly List<NotepadMarkdownTableInfo> _tables = new();
        private readonly Dictionary<int, NotepadMarkdownTableInfo> _tableByLine = new();

        internal void SetLineInfo(int lineNumber, NotepadMarkdownBlockInfo info)
            => _lineInfos[lineNumber] = info;

        internal NotepadMarkdownBlockInfo GetLineInfo(int lineNumber)
            => _lineInfos.TryGetValue(lineNumber, out var info) ? info : NotepadMarkdownBlockInfo.Normal;

        internal void AddTable(NotepadMarkdownTableInfo table)
        {
            _tables.Add(table);
            for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
                _tableByLine[ln] = table;
        }

        internal NotepadMarkdownTableInfo? GetTableForLine(int lineNumber)
            => _tableByLine.TryGetValue(lineNumber, out var table) ? table : null;

        internal IReadOnlyList<NotepadMarkdownTableInfo> Tables => _tables;
        internal IReadOnlyDictionary<int, NotepadMarkdownBlockInfo> Lines => _lineInfos;
    }
}
