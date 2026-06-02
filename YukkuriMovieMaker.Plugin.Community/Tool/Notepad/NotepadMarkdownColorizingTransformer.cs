using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownColorizingTransformer : DocumentColorizingTransformer
    {
        private const double HideEmSize = 0.01;

        private static readonly SolidColorBrush HiddenBrush = new(Colors.Transparent);
        private static readonly SolidColorBrush CodeBackground = new(Color.FromArgb(30, 128, 128, 128));
        private static readonly SolidColorBrush LinkForegroundLight = new(Color.FromRgb(0x00, 0x66, 0xCC));
        private static readonly SolidColorBrush LinkForegroundDark = new(Color.FromRgb(0x64, 0xB5, 0xF6));
        private static readonly SolidColorBrush BlockquoteMarkerLight = new(Color.FromRgb(0x3B, 0x88, 0xC3));
        private static readonly SolidColorBrush BlockquoteMarkerDark = new(Color.FromRgb(0x82, 0xB1, 0xFF));
        private static readonly FontFamily MonospaceFontFamily = new("Consolas, Courier New, monospace");
        private static readonly TextDecorationCollection StrikethroughDecorations;
        private static readonly TextDecorationCollection UnderlineDecorations;

        static NotepadMarkdownColorizingTransformer()
        {
            HiddenBrush.Freeze();
            CodeBackground.Freeze();
            LinkForegroundLight.Freeze();
            LinkForegroundDark.Freeze();
            BlockquoteMarkerLight.Freeze();
            BlockquoteMarkerDark.Freeze();

            var st = new TextDecorationCollection { TextDecorations.Strikethrough };
            st.Freeze();
            StrikethroughDecorations = st;

            var ul = new TextDecorationCollection { TextDecorations.Underline };
            ul.Freeze();
            UnderlineDecorations = ul;
        }

        private readonly NotepadMarkdownRenderState _state;
        private bool _isLightForeground;

        public NotepadMarkdownColorizingTransformer(NotepadMarkdownRenderState state)
        {
            _state = state;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.Length == 0) return;

            int lineNumber = line.LineNumber;
            if (lineNumber == _state.ActiveLineNumber) return;

            _isLightForeground = IsForegroundLight(
                TextElement.GetForeground(CurrentContext.TextView) as SolidColorBrush);

            var info = _state.DocumentMap.GetLineInfo(lineNumber);
            var lineText = CurrentContext.Document.GetText(line.Offset, line.Length);

            switch (info.BlockType)
            {
                case NotepadMarkdownBlockType.Heading:
                    ColorizeHeading(line, info, lineText);
                    break;
                case NotepadMarkdownBlockType.ThematicBreak:
                    ColorizeFull(line);
                    break;
                case NotepadMarkdownBlockType.FencedCodeFence:
                    ColorizeFull(line);
                    break;
                case NotepadMarkdownBlockType.FencedCodeContent:
                    ColorizeFencedCode(line);
                    break;
                case NotepadMarkdownBlockType.BlockquoteLine:
                    ColorizeBlockquote(line, info, lineText);
                    break;
                case NotepadMarkdownBlockType.TaskListItem:
                    ColorizeTaskListItem(line, info, lineText);
                    break;
                case NotepadMarkdownBlockType.TableSeparator:
                    ColorizeTableSeparatorLine(line, lineNumber);
                    break;
                case NotepadMarkdownBlockType.TableHeader:
                case NotepadMarkdownBlockType.TableRow:
                    ColorizeTableLine(line, lineNumber);
                    break;
                case NotepadMarkdownBlockType.Normal:
                    ColorizeInlineRange(line, lineText, 0);
                    break;
            }
        }

        private void ColorizeHeading(DocumentLine line, NotepadMarkdownBlockInfo info, string lineText)
        {
            int markerLen = Math.Min(info.MarkerLength, line.Length);
            if (markerLen > 0)
                HideMarkerRange(line.Offset, markerLen);

            int contentStart = info.MarkerLength;
            if (contentStart >= lineText.Length) return;

            double scale = GetHeadingScale(info.HeadingLevel);
            ChangeLinePart(line.Offset + contentStart, line.EndOffset, element =>
            {
                double baseSize = element.TextRunProperties.FontRenderingEmSize;
                element.TextRunProperties.SetFontRenderingEmSize(baseSize * scale);
                element.TextRunProperties.SetTypeface(MakeBold(element.TextRunProperties.Typeface));
            });
        }

        private void ColorizeFull(DocumentLine line)
        {
            if (line.Length == 0) return;
            ChangeLinePart(line.Offset, line.EndOffset, element =>
                element.TextRunProperties.SetForegroundBrush(HiddenBrush));
        }

        private void ColorizeFencedCode(DocumentLine line)
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetTypeface(
                    new Typeface(MonospaceFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
                element.TextRunProperties.SetBackgroundBrush(CodeBackground);
            });
        }

        private void ColorizeBlockquote(DocumentLine line, NotepadMarkdownBlockInfo info, string lineText)
        {
            int markerLen = Math.Min(info.MarkerLength, line.Length);
            if (markerLen > 0)
            {
                var markerBrush = _isLightForeground ? BlockquoteMarkerLight : BlockquoteMarkerDark;
                ChangeLinePart(line.Offset, line.Offset + markerLen, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(markerBrush);
                    element.TextRunProperties.SetTypeface(MakeBold(element.TextRunProperties.Typeface));
                });
            }
            if (info.MarkerLength < lineText.Length)
                ColorizeInlineRange(line, lineText, info.MarkerLength);
        }

        private void ColorizeTaskListItem(DocumentLine line, NotepadMarkdownBlockInfo info, string lineText)
        {
            int markerLen = Math.Min(info.MarkerLength, line.Length);
            if (markerLen > 0)
                ChangeLinePart(line.Offset, line.Offset + markerLen,
                    element => element.TextRunProperties.SetForegroundBrush(HiddenBrush));

            int contentStart = info.MarkerLength;
            if (contentStart < lineText.Length)
                ColorizeInlineRange(line, lineText, contentStart);
        }

        private void ColorizeTableSeparatorLine(DocumentLine line, int lineNumber)
        {
            var table = _state.DocumentMap.GetTableForLine(lineNumber);
            bool activeLineInTable = table != null
                && _state.ActiveLineNumber >= table.FirstLine
                && _state.ActiveLineNumber <= table.LastLine;

            if (!activeLineInTable)
                ColorizeFull(line);
        }

        private void ColorizeTableLine(DocumentLine line, int lineNumber)
        {
            var table = _state.DocumentMap.GetTableForLine(lineNumber);
            bool activeLineInTable = table != null
                && _state.ActiveLineNumber >= table.FirstLine
                && _state.ActiveLineNumber <= table.LastLine;

            if (!activeLineInTable)
                ColorizeFull(line);
        }

        private void ColorizeInlineRange(DocumentLine line, string lineText, int textStartInLine)
        {
            int available = lineText.Length - textStartInLine;
            if (available <= 0) return;

            var portion = textStartInLine > 0 ? lineText[textStartInLine..] : lineText;
            var spans = NotepadMarkdownParser.ParseInlineSpans(portion, line.Offset + textStartInLine);

            foreach (var span in spans)
                ApplyInlineSpan(span);
        }

        private void ApplyInlineSpan(NotepadMarkdownSpan span)
        {
            if (span.Length <= 0) return;
            int start = span.Offset;
            int end = span.Offset + span.Length;

            switch (span.Kind)
            {
                case NotepadMarkdownSpanKind.BoldItalicMarker:
                case NotepadMarkdownSpanKind.BoldMarker:
                case NotepadMarkdownSpanKind.ItalicMarker:
                case NotepadMarkdownSpanKind.StrikethroughMarker:
                case NotepadMarkdownSpanKind.CodeMarker:
                case NotepadMarkdownSpanKind.LinkMarker:
                    HideMarkerRange(start, span.Length);
                    break;

                case NotepadMarkdownSpanKind.BoldItalicContent:
                    ChangeLinePart(start, end, element =>
                    {
                        var tf = element.TextRunProperties.Typeface;
                        element.TextRunProperties.SetTypeface(
                            new Typeface(tf.FontFamily, FontStyles.Italic, FontWeights.Bold, tf.Stretch));
                    });
                    break;

                case NotepadMarkdownSpanKind.BoldContent:
                    ChangeLinePart(start, end,
                        element => element.TextRunProperties.SetTypeface(MakeBold(element.TextRunProperties.Typeface)));
                    break;

                case NotepadMarkdownSpanKind.ItalicContent:
                    ChangeLinePart(start, end, element =>
                    {
                        var tf = element.TextRunProperties.Typeface;
                        element.TextRunProperties.SetTypeface(
                            new Typeface(tf.FontFamily, FontStyles.Italic, tf.Weight, tf.Stretch));
                    });
                    break;

                case NotepadMarkdownSpanKind.StrikethroughContent:
                    ChangeLinePart(start, end,
                        element => element.TextRunProperties.SetTextDecorations(StrikethroughDecorations));
                    break;

                case NotepadMarkdownSpanKind.CodeContent:
                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetTypeface(
                            new Typeface(MonospaceFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
                        element.TextRunProperties.SetBackgroundBrush(CodeBackground);
                    });
                    break;

                case NotepadMarkdownSpanKind.LinkText:
                    var linkBrush = _isLightForeground ? LinkForegroundLight : LinkForegroundDark;
                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(linkBrush);
                        element.TextRunProperties.SetTextDecorations(UnderlineDecorations);
                    });
                    break;
            }
        }

        private void HideMarkerRange(int offset, int length)
        {
            if (length <= 0) return;
            ChangeLinePart(offset, offset + length, element =>
            {
                element.TextRunProperties.SetForegroundBrush(HiddenBrush);
                element.TextRunProperties.SetFontRenderingEmSize(HideEmSize);
            });
        }

        private static bool IsForegroundLight(SolidColorBrush? foreground)
        {
            if (foreground != null)
            {
                var c = foreground.Color;
                return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 128.0;
            }
            return true;
        }

        private static Typeface MakeBold(Typeface typeface)
            => new(typeface.FontFamily, typeface.Style, FontWeights.Bold, typeface.Stretch);

        private static double GetHeadingScale(int level) => level switch
        {
            1 => 2.0,
            2 => 1.75,
            3 => 1.5,
            4 => 1.25,
            5 => 1.1,
            _ => 1.0,
        };
    }
}
