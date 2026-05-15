using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Xaml.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class AvalonEditBehavior : Behavior<TextEditor>
    {
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(AvalonEditBehavior),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public bool WordWrap
        {
            get => (bool)GetValue(WordWrapProperty);
            set => SetValue(WordWrapProperty, value);
        }
        public static readonly DependencyProperty WordWrapProperty =
            DependencyProperty.Register(
                nameof(WordWrap),
                typeof(bool),
                typeof(AvalonEditBehavior),
                new PropertyMetadata(false, OnWordWrapChanged));

        public double ImageScale
        {
            get => (double)GetValue(ImageScaleProperty);
            set => SetValue(ImageScaleProperty, value);
        }
        public static readonly DependencyProperty ImageScaleProperty =
            DependencyProperty.Register(
                nameof(ImageScale),
                typeof(double),
                typeof(AvalonEditBehavior),
                new PropertyMetadata(1.0, OnImageScaleChanged));

        private bool _isSyncing;
        private NotepadImageElementGenerator? _imageGenerator;
        private SearchPanel? _searchPanel;
        private NotepadViewModel? _attachedViewModel;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += OnEditorTextChanged;
            AssociatedObject.PreviewDragOver += OnPreviewDragOver;
            AssociatedObject.PreviewDrop += OnPreviewDrop;
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AssociatedObject.TextArea.PreviewKeyDown += OnTextAreaPreviewKeyDown;

            CommandManager.AddPreviewCanExecuteHandler(AssociatedObject.TextArea, OnPastePreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(AssociatedObject.TextArea, OnPastePreviewExecuted);

            _imageGenerator = new NotepadImageElementGenerator { Scale = ImageScale };
            AssociatedObject.TextArea.TextView.ElementGenerators.Add(_imageGenerator);

            _searchPanel = SearchPanel.Install(AssociatedObject);

            AttachViewModel(AssociatedObject.DataContext as NotepadViewModel);

            SyncTextToEditor(Text);
            AssociatedObject.WordWrap = WordWrap;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= OnEditorTextChanged;
            AssociatedObject.PreviewDragOver -= OnPreviewDragOver;
            AssociatedObject.PreviewDrop -= OnPreviewDrop;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
            AssociatedObject.TextArea.PreviewKeyDown -= OnTextAreaPreviewKeyDown;

            CommandManager.RemovePreviewCanExecuteHandler(AssociatedObject.TextArea, OnPastePreviewCanExecute);
            CommandManager.RemovePreviewExecutedHandler(AssociatedObject.TextArea, OnPastePreviewExecuted);

            AttachViewModel(null);

            if (_imageGenerator is not null)
            {
                AssociatedObject.TextArea.TextView.ElementGenerators.Remove(_imageGenerator);
                _imageGenerator = null;
            }

            _searchPanel?.Uninstall();
            _searchPanel = null;

            base.OnDetaching();
        }

        private void OnTextAreaPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (AssociatedObject is null || AssociatedObject.IsReadOnly)
                return;
            if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;
            if (NotepadClipboardHandler.TryHandleClipboard(AssociatedObject.TextArea))
                e.Handled = true;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachViewModel(e.NewValue as NotepadViewModel);
        }

        private void AttachViewModel(NotepadViewModel? viewModel)
        {
            if (ReferenceEquals(_attachedViewModel, viewModel))
                return;
            if (_attachedViewModel is not null)
                _attachedViewModel.ImageInsertRequested -= OnImageInsertRequested;
            _attachedViewModel = viewModel;
            if (_attachedViewModel is not null)
                _attachedViewModel.ImageInsertRequested += OnImageInsertRequested;
        }

        private void OnImageInsertRequested(object? sender, NotepadImageInsertRequestedEventArgs e)
        {
            if (AssociatedObject is null || AssociatedObject.IsReadOnly)
                return;
            NotepadClipboardHandler.InsertImageFromFile(AssociatedObject.TextArea, e.FilePath);
        }

        private void OnPastePreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste)
                return;
            if (AssociatedObject is null || AssociatedObject.IsReadOnly)
                return;
            if (NotepadClipboardHandler.ClipboardContainsHandleableImage())
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void OnPastePreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste)
                return;
            if (AssociatedObject is null || AssociatedObject.IsReadOnly)
                return;
            if (NotepadClipboardHandler.TryHandleClipboard(AssociatedObject.TextArea))
                e.Handled = true;
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (NotepadClipboardHandler.CanHandleDataObject(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnPreviewDrop(object sender, DragEventArgs e)
        {
            if (AssociatedObject.IsReadOnly)
                return;
            if (NotepadClipboardHandler.TryHandleDataObject(AssociatedObject.TextArea, e.Data))
                e.Handled = true;
        }

        private static void OnWordWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvalonEditBehavior behavior && behavior.AssociatedObject is not null)
                behavior.AssociatedObject.WordWrap = (bool)e.NewValue;
        }

        private static void OnImageScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AvalonEditBehavior behavior || behavior._imageGenerator is null || behavior.AssociatedObject is null)
                return;
            behavior._imageGenerator.Scale = (double)e.NewValue;
            behavior.AssociatedObject.TextArea.TextView.Redraw();
        }

        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            SyncEditorToText();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AvalonEditBehavior behavior || behavior._isSyncing)
                return;
            behavior.SyncTextToEditor(e.NewValue as string ?? string.Empty);
        }

        private void SyncEditorToText()
        {
            if (_isSyncing)
                return;
            _isSyncing = true;
            try
            {
                Text = AssociatedObject.Text;
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void SyncTextToEditor(string text)
        {
            if (AssociatedObject is null)
                return;
            _isSyncing = true;
            try
            {
                AssociatedObject.Text = text;
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
