using System.Collections.Generic;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using YukkuriMovieMaker.Controls.AvalonEdit.FoldingStrategy;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed partial class LuaFoldingStrategy : IFoldingStrategy
    {
        [GeneratedRegex(
            @"--\[(?<lc>=*)\[.*?\]\k<lc>\]" +
            @"|\[(?<ls>=*)\[.*?\]\k<ls>\]" +
            @"|--[^\r\n]*" +
            @"|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'" +
            @"|(?<repeat>\brepeat\b)" +
            @"|(?<until>\buntil\b)" +
            @"|(?<open>\b(?:function|do|if)\b)" +
            @"|(?<close>\bend\b)",
            RegexOptions.Singleline)]
        private static partial Regex TokenPattern();

        private readonly record struct Frame(int Offset, bool IsRepeat);

        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            manager.UpdateFoldings(CreateNewFoldings(document, out int firstErrorOffset), firstErrorOffset);
        }

        private static IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            var text = document.Text;
            var newFoldings = new List<NewFolding>();
            var openStack = new Stack<Frame>();

            foreach (Match m in TokenPattern().Matches(text))
            {
                if (m.Groups["repeat"].Success)
                {
                    openStack.Push(new Frame(m.Index + m.Length, IsRepeat: true));
                }
                else if (m.Groups["open"].Success)
                {
                    openStack.Push(new Frame(m.Index + m.Length, IsRepeat: false));
                }
                else if (m.Groups["until"].Success)
                {
                    if (openStack.Count > 0 && openStack.Peek().IsRepeat)
                    {
                        var frame = openStack.Pop();
                        int blockEnd = m.Index + m.Length;
                        if (document.GetLineByOffset(frame.Offset).LineNumber <
                            document.GetLineByOffset(blockEnd).LineNumber)
                        {
                            newFoldings.Add(new NewFolding(frame.Offset, blockEnd));
                        }
                    }
                    else
                    {
                        firstErrorOffset = m.Index;
                    }
                }
                else if (m.Groups["close"].Success)
                {
                    if (openStack.Count > 0 && !openStack.Peek().IsRepeat)
                    {
                        var frame = openStack.Pop();
                        int blockEnd = m.Index + m.Length;
                        if (document.GetLineByOffset(frame.Offset).LineNumber <
                            document.GetLineByOffset(blockEnd).LineNumber)
                        {
                            newFoldings.Add(new NewFolding(frame.Offset, blockEnd));
                        }
                    }
                    else
                    {
                        firstErrorOffset = m.Index;
                    }
                }
            }

            if (firstErrorOffset < 0 && openStack.Count > 0)
                firstErrorOffset = openStack.Peek().Offset;

            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }
    }
}
