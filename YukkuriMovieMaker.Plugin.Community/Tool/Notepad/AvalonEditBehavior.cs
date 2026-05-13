using System.Windows;
using ICSharpCode.AvalonEdit;
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

        private bool _isSyncing;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += OnEditorTextChanged;

            SyncTextToEditor(Text);
            AssociatedObject.WordWrap = WordWrap;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= OnEditorTextChanged;
            base.OnDetaching();
        }
        
        private static void OnWordWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvalonEditBehavior behavior && behavior.AssociatedObject is not null)
                behavior.AssociatedObject.WordWrap = (bool)e.NewValue;
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
