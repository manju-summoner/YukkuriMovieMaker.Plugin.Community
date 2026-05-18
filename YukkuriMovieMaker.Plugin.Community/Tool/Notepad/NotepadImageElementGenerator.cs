using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Rendering;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadImageElementGenerator : VisualLineElementGenerator
    {
        private readonly NotepadImageStore store;
        private readonly double baseMaxWidth;
        private readonly double baseMaxHeight;

        public double Scale { get; set; } = 1.0;

        public NotepadImageElementGenerator(NotepadImageStore store, double baseMaxWidth = 480, double baseMaxHeight = 360)
        {
            this.store = store;
            this.baseMaxWidth = baseMaxWidth;
            this.baseMaxHeight = baseMaxHeight;
        }

        private (Match Match, int DocumentIndex)? FindMatch(int startOffset)
        {
            var endOffset = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            if (endOffset <= startOffset)
                return null;
            var relevantText = CurrentContext.GetText(startOffset, endOffset - startOffset);
            var match = NotepadImagePlaceholder.Pattern.Match(relevantText.Text, relevantText.Offset, relevantText.Count);
            if (!match.Success)
                return null;
            var documentIndex = startOffset + (match.Index - relevantText.Offset);
            return (match, documentIndex);
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            var result = FindMatch(startOffset);
            return result?.DocumentIndex ?? -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            var result = FindMatch(offset);
            if (result is null || result.Value.DocumentIndex != offset)
                return null;

            var match = result.Value.Match;
            var id = match.Groups[1].Value.ToLowerInvariant();
            if (!store.TryGet(id, out var reference))
                return null;
            var bitmap = store.GetBitmap(id);
            if (bitmap is null)
                return null;

            var (width, height) = ComputeFitSize(bitmap.PixelWidth, bitmap.PixelHeight);
            var container = BuildVisual(bitmap, reference, width, height);
            return new InlineObjectElement(match.Length, container);
        }

        private static FrameworkElement BuildVisual(BitmapSource bitmap, NotepadImageReference reference, double width, double height)
        {
            var image = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
            };
            var border = new Border
            {
                Child = image,
                Background = Brushes.Transparent,
                ToolTip = $"{bitmap.PixelWidth} x {bitmap.PixelHeight}\r\n{Texts.OpenWithCtrlClick}",
                Focusable = false,
            };
            border.QueryCursor += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                    return;
                e.Cursor = Cursors.Hand;
                e.Handled = true;
            };
            var armed = false;
            border.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                    return;
                armed = true;
                e.Handled = true;
            };
            border.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (!armed)
                    return;
                armed = false;
                e.Handled = true;
                try
                {
                    var tempPath = WriteToTempFile(reference);
                    Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Log.Default.Write($"{Texts.FailedToOpenFile}: {reference.Id}", ex);
                }
            };
            border.MouseLeave += (_, _) => armed = false;
            return border;
        }

        private static string WriteToTempFile(NotepadImageReference reference)
        {
            var dir = AppDirectories.TemporaryDirectory;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var tempPath = Path.Combine(dir, $"ymm_notepad_{reference.Id}{reference.Extension}");
            if (!File.Exists(tempPath))
                File.WriteAllBytes(tempPath, reference.Data);
            return tempPath;
        }

        private (double Width, double Height) ComputeFitSize(int pixelWidth, int pixelHeight)
        {
            var maxW = baseMaxWidth * Scale;
            var maxH = baseMaxHeight * Scale;
            if (pixelWidth <= 0 || pixelHeight <= 0)
                return (maxW, maxH);
            double w = pixelWidth;
            double h = pixelHeight;
            var scale = Math.Min(1.0, Math.Min(maxW / w, maxH / h));
            return (w * scale, h * scale);
        }
    }
}
