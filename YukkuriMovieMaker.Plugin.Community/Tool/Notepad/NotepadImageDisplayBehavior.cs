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

        protected override void OnAttached()
        {
            base.OnAttached();
            _imageGenerator = new NotepadImageElementGenerator { Scale = ImageScale };
            AssociatedObject.TextArea.TextView.ElementGenerators.Add(_imageGenerator);
        }

        protected override void OnDetaching()
        {
            if (_imageGenerator is not null)
            {
                AssociatedObject.TextArea.TextView.ElementGenerators.Remove(_imageGenerator);
                _imageGenerator = null;
            }
            base.OnDetaching();
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
