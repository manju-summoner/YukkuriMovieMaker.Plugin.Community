using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static partial class NotepadMarkdownParser
    {
        [GeneratedRegex(@"^(#{1,6})(?:[ \t]|$)")]
        private static partial Regex HeadingPattern();

        [GeneratedRegex(@"^\s{0,3}(?:(?:-[ \t]?){3,}|(?:\*[ \t]?){3,}|(?:_[ \t]?){3,})\s*$")]
        private static partial Regex ThematicBreakPattern();

        [GeneratedRegex(@"^\s{0,3}(>\s?)")]
        private static partial Regex BlockquotePattern();

        [GeneratedRegex(@"^\s{0,3}[-*+][ \t]+\[([ xX])\](?:[ \t]|$)")]
        private static partial Regex TaskListPattern();

        [GeneratedRegex(@"^\s{0,3}(`{3,}|~{3,})")]
        private static partial Regex FencedCodeFencePattern();

        [GeneratedRegex(@"^\|.+")]
        private static partial Regex TableRowPattern();

        [GeneratedRegex(@"^\|(\s*:?-+:?\s*\|)+\s*$")]
        private static partial Regex TableSeparatorPattern();

        [GeneratedRegex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Singleline)]
        private static partial Regex BoldItalicStarPattern();

        [GeneratedRegex(@"___(.+?)___", RegexOptions.Singleline)]
        private static partial Regex BoldItalicUnderPattern();

        [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
        private static partial Regex BoldStarPattern();

        [GeneratedRegex(@"__(.+?)__", RegexOptions.Singleline)]
        private static partial Regex BoldUnderPattern();

        [GeneratedRegex(@"\*([^*\r\n]+?)\*")]
        private static partial Regex ItalicStarPattern();

        [GeneratedRegex(@"_([^_\r\n]+?)_")]
        private static partial Regex ItalicUnderPattern();

        [GeneratedRegex(@"~~(.+?)~~", RegexOptions.Singleline)]
        private static partial Regex StrikethroughPattern();

        [GeneratedRegex(@"\[([^\]\r\n]+)\]\(([^)\r\n]*)\)")]
        private static partial Regex LinkPattern();

        public static NotepadMarkdownDocumentMap ParseDocument(IDocument document)
        {
            var map = new NotepadMarkdownDocumentMap();
            bool inFencedCode = false;
            char fenceChar = '`';
            int fenceMinLength = 3;

            var tableAccumulator = new List<(int lineNum, string text, bool isSeparator)>();

            for (int lineNum = 1; lineNum <= document.LineCount; lineNum++)
            {
                var docLine = document.GetLineByNumber(lineNum);
                var text = document.GetText(docLine.Offset, docLine.Length);
                NotepadMarkdownBlockInfo info;

                if (inFencedCode)
                {
                    FlushTableAccumulator(tableAccumulator, map);
                    var fenceMatch = FencedCodeFencePattern().Match(text);
                    var markerValue = fenceMatch.Success ? fenceMatch.Groups[1].Value : string.Empty;
                    bool isClosingFence = fenceMatch.Success
                        && markerValue.Length > 0
                        && markerValue[0] == fenceChar
                        && markerValue.Length >= fenceMinLength
                        && text.Trim().Length == markerValue.Length;

                    if (isClosingFence)
                    {
                        inFencedCode = false;
                        info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.FencedCodeFence, 0, fenceMatch.Length, false);
                    }
                    else
                    {
                        info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.FencedCodeContent, 0, 0, false);
                    }
                    map.SetLineInfo(lineNum, info);
                    continue;
                }

                var tableFenceMatch = FencedCodeFencePattern().Match(text);
                if (tableFenceMatch.Success)
                {
                    FlushTableAccumulator(tableAccumulator, map);
                    var marker = tableFenceMatch.Groups[1].Value;
                    inFencedCode = true;
                    fenceChar = marker[0];
                    fenceMinLength = marker.Length;
                    info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.FencedCodeFence, 0, tableFenceMatch.Length, false);
                    map.SetLineInfo(lineNum, info);
                    continue;
                }

                if (TableSeparatorPattern().IsMatch(text))
                {
                    tableAccumulator.Add((lineNum, text, true));
                    map.SetLineInfo(lineNum, new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.TableSeparator, 0, text.Length, false));
                    continue;
                }

                if (TableRowPattern().IsMatch(text))
                {
                    tableAccumulator.Add((lineNum, text, false));
                    map.SetLineInfo(lineNum, new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.TableRow, 0, 0, false));
                    continue;
                }

                FlushTableAccumulator(tableAccumulator, map);

                if (HeadingPattern().Match(text) is { Success: true } hm)
                {
                    int level = hm.Groups[1].Length;
                    info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.Heading, level, hm.Length, false);
                }
                else if (ThematicBreakPattern().IsMatch(text))
                {
                    info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.ThematicBreak, 0, text.Length, false);
                }
                else if (TaskListPattern().Match(text) is { Success: true } tlm)
                {
                    bool isChecked = tlm.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
                    info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.TaskListItem, 0, tlm.Length, isChecked);
                }
                else if (BlockquotePattern().Match(text) is { Success: true } bqm)
                {
                    info = new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.BlockquoteLine, 0, bqm.Length, false);
                }
                else
                {
                    info = NotepadMarkdownBlockInfo.Normal;
                }

                map.SetLineInfo(lineNum, info);
            }

            FlushTableAccumulator(tableAccumulator, map);
            return map;
        }

        private static void FlushTableAccumulator(
            List<(int lineNum, string text, bool isSeparator)> accumulator,
            NotepadMarkdownDocumentMap map)
        {
            if (accumulator.Count == 0) return;

            int separatorIndex = accumulator.FindIndex(x => x.isSeparator);
            bool isValidTable = separatorIndex == 1 && accumulator.Count >= 2;

            if (isValidTable)
            {
                var separatorAlignments = ParseAlignments(accumulator[separatorIndex].text);

                int columnCount = separatorAlignments.Count;
                foreach (var (_, text, isSep) in accumulator)
                {
                    if (isSep) continue;
                    columnCount = Math.Max(columnCount, SplitTableCells(text).Count);
                }
                columnCount = Math.Max(columnCount, 1);

                var alignments = new List<NotepadMarkdownTableAlignment>(columnCount);
                alignments.AddRange(separatorAlignments);
                while (alignments.Count < columnCount)
                    alignments.Add(NotepadMarkdownTableAlignment.Left);

                var rows = new List<IReadOnlyList<string>>();
                int separatorLineNum = accumulator[separatorIndex].lineNum;
                int firstLineNum = accumulator[0].lineNum;
                int lastLineNum = accumulator[^1].lineNum;

                map.SetLineInfo(firstLineNum, new NotepadMarkdownBlockInfo(NotepadMarkdownBlockType.TableHeader, 0, 0, false));

                foreach (var (lineNum, text, isSep) in accumulator)
                {
                    if (isSep) continue;
                    rows.Add(ParseTableCells(text, columnCount));
                }

                var tableInfo = new NotepadMarkdownTableInfo
                {
                    FirstLine = firstLineNum,
                    LastLine = lastLineNum,
                    SeparatorLine = separatorLineNum,
                    Rows = rows,
                    Alignments = alignments,
                    ColumnCount = columnCount,
                };
                map.AddTable(tableInfo);
            }
            else
            {
                foreach (var (lineNum, _, _) in accumulator)
                    map.SetLineInfo(lineNum, NotepadMarkdownBlockInfo.Normal);
            }

            accumulator.Clear();
        }

        private static IReadOnlyList<NotepadMarkdownTableAlignment> ParseAlignments(string separatorRow)
        {
            var alignments = new List<NotepadMarkdownTableAlignment>();
            var cells = SplitTableCells(separatorRow);
            foreach (var cell in cells)
            {
                var trimmed = cell.Trim();
                bool left = trimmed.StartsWith(':');
                bool right = trimmed.EndsWith(':');
                alignments.Add((left, right) switch
                {
                    (true, true) => NotepadMarkdownTableAlignment.Center,
                    (false, true) => NotepadMarkdownTableAlignment.Right,
                    _ => NotepadMarkdownTableAlignment.Left,
                });
            }
            return alignments;
        }

        private static IReadOnlyList<string> ParseTableCells(string rowText, int columnCount)
        {
            var raw = SplitTableCells(rowText);
            var cells = new List<string>();
            for (int i = 0; i < columnCount; i++)
                cells.Add(i < raw.Count ? raw[i].Trim() : string.Empty);
            return cells;
        }

        private static IReadOnlyList<string> SplitTableCells(string rowText)
        {
            var cells = new List<string>();
            var trimmed = rowText.Trim();
            if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
            if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
            foreach (var cell in trimmed.Split('|'))
                cells.Add(cell);
            return cells;
        }

        public static bool TryFindLinkAtOffset(string lineText, int charOffset, out string url)
        {
            url = string.Empty;
            if (string.IsNullOrEmpty(lineText)) return false;
            foreach (Match m in LinkPattern().Matches(lineText))
            {
                if (m.Index <= charOffset && charOffset < m.Index + m.Length)
                {
                    url = m.Groups[2].Value;
                    return !string.IsNullOrEmpty(url);
                }
            }
            return false;
        }

        public static string? FindLinkUrlAtOffset(string lineText, int charOffset)
            => TryFindLinkAtOffset(lineText, charOffset, out var url) ? url : null;

        public static IReadOnlyList<NotepadMarkdownSpan> ParseInlineSpans(string text, int baseOffset)
        {
            if (string.IsNullOrEmpty(text))
                return [];

            var spans = new List<NotepadMarkdownSpan>();
            var claimed = new bool[text.Length];

            ProcessCodeSpans(text, baseOffset, spans, claimed);
            ProcessDelimitedPattern(BoldItalicStarPattern(), text, baseOffset, spans, claimed, 3,
                NotepadMarkdownSpanKind.BoldItalicMarker, NotepadMarkdownSpanKind.BoldItalicContent);
            ProcessDelimitedPattern(BoldItalicUnderPattern(), text, baseOffset, spans, claimed, 3,
                NotepadMarkdownSpanKind.BoldItalicMarker, NotepadMarkdownSpanKind.BoldItalicContent);
            ProcessDelimitedPattern(BoldStarPattern(), text, baseOffset, spans, claimed, 2,
                NotepadMarkdownSpanKind.BoldMarker, NotepadMarkdownSpanKind.BoldContent);
            ProcessDelimitedPattern(BoldUnderPattern(), text, baseOffset, spans, claimed, 2,
                NotepadMarkdownSpanKind.BoldMarker, NotepadMarkdownSpanKind.BoldContent);
            ProcessDelimitedPattern(ItalicStarPattern(), text, baseOffset, spans, claimed, 1,
                NotepadMarkdownSpanKind.ItalicMarker, NotepadMarkdownSpanKind.ItalicContent);
            ProcessDelimitedPattern(ItalicUnderPattern(), text, baseOffset, spans, claimed, 1,
                NotepadMarkdownSpanKind.ItalicMarker, NotepadMarkdownSpanKind.ItalicContent);
            ProcessDelimitedPattern(StrikethroughPattern(), text, baseOffset, spans, claimed, 2,
                NotepadMarkdownSpanKind.StrikethroughMarker, NotepadMarkdownSpanKind.StrikethroughContent);
            ProcessLinkSpans(text, baseOffset, spans, claimed);

            return spans;
        }

        private static void ProcessCodeSpans(string text, int baseOffset, List<NotepadMarkdownSpan> spans, bool[] claimed)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != '`' || IsRangeClaimed(claimed, i, 1))
                {
                    i++;
                    continue;
                }

                int openEnd = i + 1;
                while (openEnd < text.Length && text[openEnd] == '`') openEnd++;
                int tickLen = openEnd - i;
                var closePattern = new string('`', tickLen);

                int closeStart = -1;
                int searchPos = openEnd;
                while (searchPos <= text.Length - tickLen)
                {
                    int found = text.IndexOf(closePattern, searchPos, StringComparison.Ordinal);
                    if (found < 0) break;
                    bool isExact = found + tickLen >= text.Length || text[found + tickLen] != '`';
                    if (isExact && !IsRangeClaimed(claimed, found, tickLen))
                    {
                        closeStart = found;
                        break;
                    }
                    searchPos = found + 1;
                }

                if (closeStart >= 0)
                {
                    int contentLen = closeStart - openEnd;
                    SetRangeClaimed(claimed, i, tickLen);
                    if (contentLen > 0) SetRangeClaimed(claimed, openEnd, contentLen);
                    SetRangeClaimed(claimed, closeStart, tickLen);

                    spans.Add(new NotepadMarkdownSpan(baseOffset + i, tickLen, NotepadMarkdownSpanKind.CodeMarker));
                    if (contentLen > 0)
                        spans.Add(new NotepadMarkdownSpan(baseOffset + openEnd, contentLen, NotepadMarkdownSpanKind.CodeContent));
                    spans.Add(new NotepadMarkdownSpan(baseOffset + closeStart, tickLen, NotepadMarkdownSpanKind.CodeMarker));
                    i = closeStart + tickLen;
                }
                else
                {
                    i++;
                }
            }
        }

        private static void ProcessDelimitedPattern(
            Regex pattern, string text, int baseOffset,
            List<NotepadMarkdownSpan> spans, bool[] claimed,
            int markerLen, NotepadMarkdownSpanKind markerKind, NotepadMarkdownSpanKind contentKind)
        {
            foreach (Match m in pattern.Matches(text))
            {
                if (IsRangeClaimed(claimed, m.Index, m.Length)) continue;
                int contentLen = m.Groups[1].Length;
                if (contentLen <= 0) continue;

                SetRangeClaimed(claimed, m.Index, markerLen);
                SetRangeClaimed(claimed, m.Index + markerLen, contentLen);
                SetRangeClaimed(claimed, m.Index + m.Length - markerLen, markerLen);

                spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index, markerLen, markerKind));
                spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index + markerLen, contentLen, contentKind));
                spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index + m.Length - markerLen, markerLen, markerKind));
            }
        }

        private static void ProcessLinkSpans(string text, int baseOffset, List<NotepadMarkdownSpan> spans, bool[] claimed)
        {
            foreach (Match m in LinkPattern().Matches(text))
            {
                if (IsRangeClaimed(claimed, m.Index, m.Length)) continue;
                SetRangeClaimed(claimed, m.Index, m.Length);

                int textLen = m.Groups[1].Length;
                int suffixStart = 1 + textLen;
                int suffixLen = m.Length - textLen - 1;

                spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index, 1, NotepadMarkdownSpanKind.LinkMarker));
                if (textLen > 0)
                    spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index + 1, textLen, NotepadMarkdownSpanKind.LinkText));
                if (suffixLen > 0)
                    spans.Add(new NotepadMarkdownSpan(baseOffset + m.Index + suffixStart, suffixLen, NotepadMarkdownSpanKind.LinkMarker));
            }
        }

        private static bool IsRangeClaimed(bool[] claimed, int start, int length)
        {
            int end = Math.Min(start + length, claimed.Length);
            for (int i = start; i < end; i++)
                if (claimed[i]) return true;
            return false;
        }

        private static void SetRangeClaimed(bool[] claimed, int start, int length)
        {
            int end = Math.Min(start + length, claimed.Length);
            for (int i = start; i < end; i++)
                claimed[i] = true;
        }
    }
}
