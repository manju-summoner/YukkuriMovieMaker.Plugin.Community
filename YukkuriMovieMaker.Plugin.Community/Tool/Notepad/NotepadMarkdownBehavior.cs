using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Xaml.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal sealed class NotepadMarkdownBehavior : Behavior<TextEditor>
    {
        public bool IsMarkdown
        {
            get => (bool)GetValue(IsMarkdownProperty);
            set => SetValue(IsMarkdownProperty, value);
        }
        public static readonly DependencyProperty IsMarkdownProperty =
            DependencyProperty.Register(
                nameof(IsMarkdown),
                typeof(bool),
                typeof(NotepadMarkdownBehavior),
                new PropertyMetadata(false, OnIsMarkdownChanged));

        private NotepadMarkdownRenderState? _state;
        private NotepadMarkdownColorizingTransformer? _transformer;
        private NotepadMarkdownBackgroundRenderer? _renderer;
        private int _previousActiveLine = -1;
        private bool _attached;
        private Cursor? _savedCursor;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            AssociatedObject.Document.Changed += OnDocumentChanged;

            if (IsMarkdown)
                AttachMarkdown();
        }

        protected override void OnDetaching()
        {
            DetachMarkdown();
            AssociatedObject.Document.Changed -= OnDocumentChanged;
            AssociatedObject.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            base.OnDetaching();
        }

        private static void OnIsMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not NotepadMarkdownBehavior behavior || behavior.AssociatedObject is null) return;

            if ((bool)e.NewValue)
                behavior.AttachMarkdown();
            else
                behavior.DetachMarkdown();
        }

        private void OnCaretPositionChanged(object? sender, EventArgs e)
        {
            if (!_attached || _state is null || AssociatedObject is null) return;

            int newLine = AssociatedObject.TextArea.Caret.Line;
            if (newLine == _previousActiveLine) return;

            int oldLine = _previousActiveLine;
            _state.ActiveLineNumber = newLine;
            _previousActiveLine = newLine;

            RedrawTableRegion(oldLine);
            RedrawTableRegion(newLine);
            RedrawDocumentLine(oldLine);
            RedrawDocumentLine(newLine);
        }

        private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
        {
            if (!_attached) return;
            RebuildDocumentMap();
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_attached || _state is null || AssociatedObject is null) return;

            bool ctrlHeld = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!ctrlHeld)
            {
                RestoreCursor();
                return;
            }

            UpdateLinkCursor(e.GetPosition(AssociatedObject.TextArea.TextView));
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_attached || AssociatedObject is null) return;
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
            {
                var pos = Mouse.GetPosition(AssociatedObject.TextArea.TextView);
                UpdateLinkCursor(pos);
            }
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!_attached || AssociatedObject is null) return;
            if (e.Key is Key.LeftCtrl or Key.RightCtrl)
                RestoreCursor();
        }

        private void UpdateLinkCursor(Point positionInTextView)
        {
            if (AssociatedObject?.Document is null) return;

            var textView = AssociatedObject.TextArea.TextView;
            var pos = textView.GetPositionFloor(positionInTextView + textView.ScrollOffset);

            bool overLink = false;
            if (pos.HasValue)
            {
                int lineNumber = pos.Value.Line;
                if (lineNumber >= 1 && lineNumber <= AssociatedObject.Document.LineCount)
                {
                    var docLine = AssociatedObject.Document.GetLineByNumber(lineNumber);
                    var lineText = AssociatedObject.Document.GetText(docLine.Offset, docLine.Length);
                    int charOffset = pos.Value.Column - 1;
                    if (charOffset >= 0 && charOffset < lineText.Length)
                        overLink = NotepadMarkdownParser.TryFindLinkAtOffset(lineText, charOffset, out _);
                }
            }

            if (overLink)
            {
                if (AssociatedObject.TextArea.Cursor != Cursors.Hand)
                {
                    _savedCursor = AssociatedObject.TextArea.Cursor;
                    AssociatedObject.TextArea.Cursor = Cursors.Hand;
                }
            }
            else
            {
                RestoreCursor();
            }
        }

        private void RestoreCursor()
        {
            if (AssociatedObject is null) return;
            if (AssociatedObject.TextArea.Cursor == Cursors.Hand)
            {
                AssociatedObject.TextArea.Cursor = _savedCursor;
                _savedCursor = null;
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (AssociatedObject?.Document is null) return;

            var textArea = AssociatedObject.TextArea;
            var pos = textArea.TextView.GetPositionFloor(
                e.GetPosition(textArea.TextView) + textArea.TextView.ScrollOffset);

            if (!pos.HasValue) return;

            int lineNumber = pos.Value.Line;
            if (lineNumber < 1 || lineNumber > AssociatedObject.Document.LineCount) return;

            var docLine = AssociatedObject.Document.GetLineByNumber(lineNumber);
            var lineText = AssociatedObject.Document.GetText(docLine.Offset, docLine.Length);
            int charOffset = pos.Value.Column - 1;
            if (charOffset < 0 || charOffset >= lineText.Length) return;

            if (!NotepadMarkdownParser.TryFindLinkAtOffset(lineText, charOffset, out var url)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto) return;

            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void AttachMarkdown()
        {
            if (_attached || AssociatedObject is null) return;

            _state = new NotepadMarkdownRenderState
            {
                ActiveLineNumber = AssociatedObject.TextArea.Caret.Line,
            };
            _previousActiveLine = _state.ActiveLineNumber;

            _transformer = new NotepadMarkdownColorizingTransformer(_state);
            _renderer = new NotepadMarkdownBackgroundRenderer(_state);

            AssociatedObject.TextArea.TextView.LineTransformers.Add(_transformer);
            AssociatedObject.TextArea.TextView.BackgroundRenderers.Add(_renderer);
            AssociatedObject.TextArea.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.TextArea.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.TextArea.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.TextArea.PreviewKeyUp += OnPreviewKeyUp;

            _attached = true;
            RebuildDocumentMap();
        }

        private void DetachMarkdown()
        {
            if (!_attached || AssociatedObject is null) return;

            RestoreCursor();

            AssociatedObject.TextArea.PreviewKeyUp -= OnPreviewKeyUp;
            AssociatedObject.TextArea.PreviewKeyDown -= OnPreviewKeyDown;
            AssociatedObject.TextArea.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.TextArea.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;

            if (_transformer is not null)
                AssociatedObject.TextArea.TextView.LineTransformers.Remove(_transformer);
            if (_renderer is not null)
                AssociatedObject.TextArea.TextView.BackgroundRenderers.Remove(_renderer);

            _transformer = null;
            _renderer = null;
            _state = null;
            _attached = false;

            AssociatedObject.TextArea.TextView.Redraw();
        }

        private void RebuildDocumentMap()
        {
            if (!_attached || _state is null || AssociatedObject?.Document is null) return;

            _state.DocumentMap = NotepadMarkdownParser.ParseDocument(AssociatedObject.Document);
            AssociatedObject.TextArea.TextView.Redraw();
        }

        private void RedrawDocumentLine(int lineNumber)
        {
            if (AssociatedObject?.Document is null) return;
            if (lineNumber < 1 || lineNumber > AssociatedObject.Document.LineCount) return;
            var docLine = AssociatedObject.Document.GetLineByNumber(lineNumber);
            AssociatedObject.TextArea.TextView.Redraw(docLine);
        }

        private void RedrawTableRegion(int lineNumber)
        {
            if (!_attached || _state is null || AssociatedObject?.Document is null) return;
            if (lineNumber < 1 || lineNumber > AssociatedObject.Document.LineCount) return;

            var table = _state.DocumentMap.GetTableForLine(lineNumber);
            if (table is null) return;

            for (int ln = table.FirstLine; ln <= table.LastLine; ln++)
            {
                if (ln < 1 || ln > AssociatedObject.Document.LineCount) continue;
                var docLine = AssociatedObject.Document.GetLineByNumber(ln);
                AssociatedObject.TextArea.TextView.Redraw(docLine);
            }
        }
    }
}
