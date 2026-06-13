using System.Windows;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadImageDisplayBehavior : Behavior<TextEditor>
    {
        public double ImageScale
        {
            get => (double)GetValue(ImageScaleProperty);
            set => SetValue(ImageScaleProperty, value);
        }
        public static readonly DependencyProperty ImageScaleProperty =
            DependencyProperty.Register(
                nameof(ImageScale),
                typeof(double),
                typeof(NotepadImageDisplayBehavior),
                new PropertyMetadata(1.0, OnImageScaleChanged));

        private NotepadImageElementGenerator? _imageGenerator;
        private NotepadViewModel? _attachedViewModel;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AttachViewModel(AssociatedObject.DataContext as NotepadViewModel);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
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
            DetachGenerator();
            _attachedViewModel = viewModel;
            if (_attachedViewModel is null || AssociatedObject is null)
                return;
            _imageGenerator = new NotepadImageElementGenerator(_attachedViewModel.ImageStore) { Scale = ImageScale };
            AssociatedObject.TextArea.TextView.ElementGenerators.Add(_imageGenerator);
            AssociatedObject.TextArea.TextView.Redraw();
        }

        private void DetachGenerator()
        {
            if (_imageGenerator is null || AssociatedObject is null)
            {
                _imageGenerator = null;
                return;
            }
            AssociatedObject.TextArea.TextView.ElementGenerators.Remove(_imageGenerator);
            _imageGenerator = null;
        }

        private static void OnImageScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not NotepadImageDisplayBehavior behavior || behavior._imageGenerator is null || behavior.AssociatedObject is null)
                return;
            behavior._imageGenerator.Scale = (double)e.NewValue;
            behavior.AssociatedObject.TextArea.TextView.Redraw();
        }
    }
}
