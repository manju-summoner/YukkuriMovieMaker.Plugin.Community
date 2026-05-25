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
            _searchPanel.Localization = new NotepadSearchLocalization();
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
            _attachedViewModel?.ToggleSearchRequested -= OnToggleSearchRequested;
            _attachedViewModel = viewModel;
            _attachedViewModel?.ToggleSearchRequested += OnToggleSearchRequested;
        }

        private void OnToggleSearchRequested(object? sender, EventArgs e)
        {
            if (_searchPanel is null)
                return;
            if (_searchPanel.IsClosed)
            {
                _searchPanel.Open();
                AssociatedObject?.TextArea?.Focus();
            }
            else
            {
                _searchPanel.Close();
            }
        }
    }
}
