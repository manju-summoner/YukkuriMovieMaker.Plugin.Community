using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadImageInputBehavior : Behavior<TextEditor>
    {
        private NotepadViewModel? _attachedViewModel;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewDragOver += OnPreviewDragOver;
            AssociatedObject.PreviewDrop += OnPreviewDrop;
            AssociatedObject.DataContextChanged += OnDataContextChanged;

            CommandManager.AddPreviewCanExecuteHandler(AssociatedObject.TextArea, OnPastePreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(AssociatedObject.TextArea, OnPastePreviewExecuted);

            AttachViewModel(AssociatedObject.DataContext as NotepadViewModel);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewDragOver -= OnPreviewDragOver;
            AssociatedObject.PreviewDrop -= OnPreviewDrop;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;

            CommandManager.RemovePreviewCanExecuteHandler(AssociatedObject.TextArea, OnPastePreviewCanExecute);
            CommandManager.RemovePreviewExecutedHandler(AssociatedObject.TextArea, OnPastePreviewExecuted);

            AttachViewModel(null);
            base.OnDetaching();
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
            if (AssociatedObject is null || AssociatedObject.IsReadOnly || _attachedViewModel is null)
                return;
            NotepadClipboardHandler.InsertImageFromFile(AssociatedObject.TextArea, e.FilePath, _attachedViewModel.ImageStore);
        }

        private void OnPastePreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (AssociatedObject is null || AssociatedObject.IsReadOnly)
                return;
            if (e.Command == ApplicationCommands.Paste && NotepadClipboardHandler.ClipboardContainsHandleableImage())
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void OnPastePreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (AssociatedObject is null || AssociatedObject.IsReadOnly || _attachedViewModel is null)
                return;
            if (e.Command == ApplicationCommands.Paste && NotepadClipboardHandler.TryHandleClipboard(AssociatedObject.TextArea, _attachedViewModel.ImageStore))
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
            if (AssociatedObject.IsReadOnly || _attachedViewModel is null)
                return;
            if (NotepadClipboardHandler.TryHandleDataObject(AssociatedObject.TextArea, e.Data, _attachedViewModel.ImageStore))
                e.Handled = true;
        }
    }
}
