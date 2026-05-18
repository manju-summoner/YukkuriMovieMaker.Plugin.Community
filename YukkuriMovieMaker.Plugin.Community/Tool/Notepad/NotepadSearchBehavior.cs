using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Xaml.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadSearchBehavior : Behavior<TextEditor>
    {
        private SearchPanel? _searchPanel;
        private NotepadViewModel? _attachedViewModel;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            _searchPanel = SearchPanel.Install(AssociatedObject);
            AttachViewModel(AssociatedObject.DataContext as NotepadViewModel);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
            AttachViewModel(null);
            _searchPanel?.Uninstall();
            _searchPanel = null;
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
                _attachedViewModel.OpenSearchRequested -= OnOpenSearchRequested;
            _attachedViewModel = viewModel;
            if (_attachedViewModel is not null)
                _attachedViewModel.OpenSearchRequested += OnOpenSearchRequested;
        }

        private void OnOpenSearchRequested(object? sender, EventArgs e)
        {
            _searchPanel?.Open();
            AssociatedObject?.TextArea?.Focus();
        }
    }
}
