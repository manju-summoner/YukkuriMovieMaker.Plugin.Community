using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownBackgroundRenderer : IBackgroundRenderer
    {
        private static readonly SolidColorBrush CodeBlockBackground;
        private static readonly SolidColorBrush TableHeaderBackground;
        private static readonly SolidColorBrush TableRowEvenBackground;
        private static readonly SolidColorBrush CheckboxCheckedFill;
        private static readonly Typeface CheckboxTypeface;

        static NotepadMarkdownBackgroundRenderer()
        {
            CodeBlockBackground = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
            CodeBlockBackground.Freeze();

            TableHeaderBackground = new SolidColorBrush(Color.FromArgb(25, 128, 128, 200));
            TableHeaderBackground.Freeze();

            TableRowEvenBackground = new SolidColorBrush(Color.FromArgb(8, 128, 128, 128));
            TableRowEvenBackground.Freeze();

            CheckboxCheckedFill = new SolidColorBrush(Color.FromArgb(220, 60, 160, 80));
            CheckboxCheckedFill.Freeze();

            CheckboxTypeface = new Typeface(
                new FontFamily("Segoe UI Symbol"),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
        }

        private readonly NotepadMarkdownRenderState _state;
        private Color _cachedForeground = Colors.Transparent;
        private Pen _horizontalRulePen = null!;
        private Pen _tableBorderPen = null!;
        private Pen _tableHeaderSeparatorPen = null!;
        private SolidColorBrush _tableCellForeground = null!;
        private Pen _checkboxStrokePen = null!;
        private double _pixelsPerDip = 1.0;

        public NotepadMarkdownBackgroundRenderer(NotepadMarkdownRenderState state)
        {
            _state = state;
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document == null) return;
            textView.EnsureVisualLines();
            if (!textView.VisualLinesValid || textView.VisualLines.Count == 0) return;

            _pixelsPerDip = VisualTreeHelper.GetDpi(textView).PixelsPerDip;
            UpdateThemeColors(textView);

            DrawCodeBlockBackgrounds(textView, drawingContext);
            DrawPerLineDecorations(textView, drawingContext);
            DrawTables(textView, drawingContext);
        }

        private void UpdateThemeColors(TextView textView)
        {
            Color fg = TextElement.GetForeground(textView) is SolidColorBrush b
                ? b.Color
                : SystemColors.WindowTextColor;
            if (fg == _cachedForeground) return;
            _cachedForeground = fg;

            var hrBrush = new SolidColorBrush(Color.FromArgb(160, fg.R, fg.G, fg.B));
            hrBrush.Freeze();
            _horizontalRulePen = new Pen(hrBrush, 1.0);
            _horizontalRulePen.Freeze();

            var tableBorderBrush = new SolidColorBrush(Color.FromArgb(180, fg.R, fg.G, fg.B));
            tableBorderBrush.Freeze();
            _tableBorderPen = new Pen(tableBorderBrush, 1.0);
            _tableBorderPen.Freeze();

            var tableHeaderSepBrush = new SolidColorBrush(Color.FromArgb(220, fg.R, fg.G, fg.B));
            tableHeaderSepBrush.Freeze();
            _tableHeaderSeparatorPen = new Pen(tableHeaderSepBrush, 2.0);
            _tableHeaderSeparatorPen.Freeze();

            _tableCellForeground = new SolidColorBrush(Color.FromArgb(220, fg.R, fg.G, fg.B));
            _tableCellForeground.Freeze();

            var checkboxStrokeBrush = new SolidColorBrush(Color.FromArgb(200, fg.R, fg.G, fg.B));
            checkboxStrokeBrush.Freeze();
            _checkboxStrokePen = new Pen(checkboxStrokeBrush, 1.5);
            _checkboxStrokePen.Freeze();
        }

        private void DrawCodeBlockBackgrounds(TextView textView, DrawingContext drawingContext)
        {
            double? blockTop = null;
            double blockBottom = 0;

            foreach (var visualLine in textView.VisualLines)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;
                var info = _state.DocumentMap.GetLineInfo(lineNumber);
                bool isCodeLine = info.BlockType is NotepadMarkdownBlockType.FencedCodeFence
                    or NotepadMarkdownBlockType.FencedCodeContent;

                double lineTop = visualLine.VisualTop - textView.ScrollOffset.Y;
                double lineBottom = lineTop + visualLine.Height;

                if (isCodeLine)
                {
                    blockTop ??= lineTop;
                    blockBottom = lineBottom;
                }
                else if (blockTop.HasValue)
                {
                    drawingContext.DrawRectangle(CodeBlockBackground, null,
                        new Rect(0, blockTop.Value, textView.ActualWidth, blockBottom - blockTop.Value));
                    blockTop = null;
                }
            }

            if (blockTop.HasValue)
            {
                drawingContext.DrawRectangle(CodeBlockBackground, null,
                    new Rect(0, blockTop.Value, textView.ActualWidth, blockBottom - blockTop.Value));
            }
        }

        private void DrawPerLineDecorations(TextView textView, DrawingContext drawingContext)
        {
            foreach (var visualLine in textView.VisualLines)
            {
                int lineNumber = visualLine.FirstDocumentLine.LineNumber;
                if (lineNumber == _state.ActiveLineNumber) continue;

                var info = _state.DocumentMap.GetLineInfo(lineNumber);
                double top = visualLine.VisualTop - textView.ScrollOffset.Y;
                double height = visualLine.Height;
                double width = textView.ActualWidth;

                switch (info.BlockType)
                {
                    case NotepadMarkdownBlockType.ThematicBreak:
                        DrawHorizontalRule(drawingContext, top, height, width);
                        break;
                    case NotepadMarkdownBlockType.TaskListItem:
                        DrawCheckbox(drawingContext, top, height, info.TaskChecked);
                        break;
                }
            }
        }

        private void DrawTables(TextView textView, DrawingContext drawingContext)
        {
            foreach (var table in _state.DocumentMap.Tables)
            {
                bool anyVisible = false;
                for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
                {
                    if (TryGetVisualLine(textView, ln, out _))
                    {
                        anyVisible = true;
                        break;
                    }
                }
                if (!anyVisible) continue;

                bool activeLineInTable = _state.ActiveLineNumber >= table.FirstLine
                    && _state.ActiveLineNumber <= table.LastLine;
                if (activeLineInTable) continue;

                DrawTableGrid(textView, drawingContext, table);
            }
        }

        private void DrawTableGrid(TextView textView, DrawingContext drawingContext, NotepadMarkdownTableInfo table)
        {
            double tableTop = double.MaxValue;
            double tableBottom = double.MinValue;
            double separatorBottom = double.MinValue;

            var rowTops = new Dictionary<int, double>();
            var rowBottoms = new Dictionary<int, double>();

            for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
            {
                if (!TryGetVisualLine(textView, ln, out var vl)) continue;
                double top = vl.VisualTop - textView.ScrollOffset.Y;
                double bottom = top + vl.Height;

                rowTops[ln] = top;
                rowBottoms[ln] = bottom;

                if (top < tableTop) tableTop = top;
                if (bottom > tableBottom) tableBottom = bottom;
                if (ln == table.SeparatorLine) separatorBottom = bottom;
            }

            if (tableTop == double.MaxValue) return;

            double tableWidth = Math.Min(textView.ActualWidth, CalculateTableWidth(table));
            double[] columnWidths = CalculateColumnWidths(table, tableWidth);
            double[] columnX = BuildColumnXPositions(columnWidths);
            double emSize = textView.DefaultLineHeight * 0.72;
            const double paddingH = 6.0;

            int dataRowIndex = 0;
            for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
            {
                if (ln == table.SeparatorLine) continue;
                if (!rowTops.TryGetValue(ln, out double rTop)) continue;
                double rHeight = rowBottoms[ln] - rTop;
                bool isHeader = table.IsHeaderLine(ln);

                if (isHeader)
                    drawingContext.DrawRectangle(TableHeaderBackground, null, new Rect(0, rTop, tableWidth, rHeight));
                else if (dataRowIndex % 2 == 1)
                    drawingContext.DrawRectangle(TableRowEvenBackground, null, new Rect(0, rTop, tableWidth, rHeight));

                int rowDataIndex = isHeader ? 0 : dataRowIndex + 1;
                if (rowDataIndex < table.Rows.Count)
                {
                    DrawTableRowText(drawingContext, table.Rows[rowDataIndex], table.Alignments,
                        columnX, columnWidths, rTop, rHeight, paddingH, isHeader, emSize);
                }

                if (!isHeader) dataRowIndex++;
            }

            drawingContext.DrawLine(_tableBorderPen, new Point(0, tableTop + 0.5), new Point(tableWidth, tableTop + 0.5));
            drawingContext.DrawLine(_tableBorderPen, new Point(0, tableBottom - 0.5), new Point(tableWidth, tableBottom - 0.5));
            drawingContext.DrawLine(_tableBorderPen, new Point(0.5, tableTop), new Point(0.5, tableBottom));
            drawingContext.DrawLine(_tableBorderPen, new Point(tableWidth - 0.5, tableTop), new Point(tableWidth - 0.5, tableBottom));

            for (int c = 1; c < columnX.Length; c++)
            {
                double x = Math.Round(columnX[c]) + 0.5;
                drawingContext.DrawLine(_tableBorderPen, new Point(x, tableTop), new Point(x, tableBottom));
            }

            for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
            {
                if (ln == table.FirstLine || ln == table.SeparatorLine) continue;
                if (!rowTops.TryGetValue(ln, out double rTop)) continue;
                double y = Math.Round(rTop) + 0.5;
                drawingContext.DrawLine(_tableBorderPen, new Point(0, y), new Point(tableWidth, y));
            }

            if (separatorBottom != double.MinValue)
            {
                double y = Math.Round(separatorBottom) - 0.5;
                drawingContext.DrawLine(_tableHeaderSeparatorPen, new Point(0, y), new Point(tableWidth, y));
            }
        }

        private void DrawTableRowText(
            DrawingContext dc,
            IReadOnlyList<string> cells,
            IReadOnlyList<NotepadMarkdownTableAlignment> alignments,
            double[] columnX,
            double[] columnWidths,
            double rowTop,
            double rowHeight,
            double paddingH,
            bool isBold,
            double emSize)
        {
            var typeface = new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                isBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            for (int c = 0; c < cells.Count && c < columnX.Length; c++)
            {
                string cellText = cells[c];
                if (string.IsNullOrEmpty(cellText)) continue;

                double cellWidth = columnWidths[c] - paddingH * 2;
                if (cellWidth <= 0) continue;

                var alignment = c < alignments.Count ? alignments[c] : NotepadMarkdownTableAlignment.Left;

                var formattedText = new FormattedText(
                    cellText,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    emSize,
                    _tableCellForeground,
                    _pixelsPerDip)
                {
                    MaxTextWidth = cellWidth,
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                };

                double x = alignment switch
                {
                    NotepadMarkdownTableAlignment.Right =>
                        columnX[c] + columnWidths[c] - paddingH - formattedText.Width,
                    NotepadMarkdownTableAlignment.Center =>
                        columnX[c] + (columnWidths[c] - formattedText.Width) / 2.0,
                    _ =>
                        columnX[c] + paddingH,
                };
                double y = rowTop + (rowHeight - formattedText.Height) / 2.0;

                dc.DrawText(formattedText, new Point(x, y));
            }
        }

        private static double CalculateTableWidth(NotepadMarkdownTableInfo table)
        {
            double total = 0;
            for (int c = 0; c < table.ColumnCount; c++)
            {
                double maxLen = 4;
                foreach (var row in table.Rows)
                {
                    if (c < row.Count)
                        maxLen = Math.Max(maxLen, row[c].Length);
                }
                total += Math.Max(60, maxLen * 7.5 + 12);
            }
            return total + 2;
        }

        private static double[] CalculateColumnWidths(NotepadMarkdownTableInfo table, double tableWidth)
        {
            var widths = new double[table.ColumnCount];
            double totalRawWidth = 0;

            for (int c = 0; c < table.ColumnCount; c++)
            {
                double maxLen = 4;
                foreach (var row in table.Rows)
                {
                    if (c < row.Count)
                        maxLen = Math.Max(maxLen, row[c].Length);
                }
                widths[c] = Math.Max(60, maxLen * 7.5 + 12);
                totalRawWidth += widths[c];
            }

            if (totalRawWidth > 0)
            {
                double scale = (tableWidth - 2) / totalRawWidth;
                for (int c = 0; c < widths.Length; c++)
                    widths[c] *= scale;
            }

            return widths;
        }

        private static double[] BuildColumnXPositions(double[] columnWidths)
        {
            var x = new double[columnWidths.Length];
            double current = 0;
            for (int c = 0; c < columnWidths.Length; c++)
            {
                x[c] = current;
                current += columnWidths[c];
            }
            return x;
        }

        private static bool TryGetVisualLine(TextView textView, int lineNumber, out VisualLine visualLine)
        {
            foreach (var vl in textView.VisualLines)
            {
                if (vl.FirstDocumentLine.LineNumber == lineNumber)
                {
                    visualLine = vl;
                    return true;
                }
            }
            visualLine = null!;
            return false;
        }

        private void DrawHorizontalRule(DrawingContext dc, double top, double height, double width)
        {
            double y = Math.Round(top + height / 2.0) + 0.5;
            dc.DrawLine(_horizontalRulePen, new Point(0, y), new Point(width, y));
        }

        private void DrawCheckbox(DrawingContext dc, double top, double height, bool isChecked)
        {
            string glyph = isChecked ? "\u2611" : "\u2610";
            var brush = isChecked ? CheckboxCheckedFill : _tableCellForeground;
            double emSize = height * 0.72;

            var ft = new FormattedText(
                glyph,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                CheckboxTypeface,
                emSize,
                brush,
                _pixelsPerDip);

            const double margin = 2.0;
            double cx = margin;
            double cy = top + (height - ft.Height) / 2.0;
            dc.DrawText(ft, new Point(cx, cy));
        }
    }
}
