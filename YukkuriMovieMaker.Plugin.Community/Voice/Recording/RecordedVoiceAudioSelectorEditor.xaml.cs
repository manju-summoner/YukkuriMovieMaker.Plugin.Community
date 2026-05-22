using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public partial class RecordedVoiceAudioSelectorEditor : UserControl, IPropertyEditorControl2
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public RecordedVoiceParameter? Parameter
        {
            get => (RecordedVoiceParameter?)GetValue(ParameterProperty);
            set => SetValue(ParameterProperty, value);
        }

        public static readonly DependencyProperty ParameterProperty =
            DependencyProperty.Register(nameof(Parameter), typeof(RecordedVoiceParameter), typeof(RecordedVoiceAudioSelectorEditor), new PropertyMetadata(null, OnParameterChanged));

        public string SelectedFileLabel
        {
            get => (string)GetValue(SelectedFileLabelProperty);
            set => SetValue(SelectedFileLabelProperty, value);
        }

        public static readonly DependencyProperty SelectedFileLabelProperty =
            DependencyProperty.Register(nameof(SelectedFileLabel), typeof(string), typeof(RecordedVoiceAudioSelectorEditor), new PropertyMetadata(Texts.Unselected));

        public string SelectedFilePath
        {
            get => (string)GetValue(SelectedFilePathProperty);
            set => SetValue(SelectedFilePathProperty, value);
        }

        public static readonly DependencyProperty SelectedFilePathProperty =
            DependencyProperty.Register(nameof(SelectedFilePath), typeof(string), typeof(RecordedVoiceAudioSelectorEditor), new PropertyMetadata(string.Empty));

        public RecordedVoiceAudioSelectorEditor()
        {
            InitializeComponent();
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            // This editor does not require editor-specific context.
        }

        private static void OnParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RecordedVoiceAudioSelectorEditor editor)
                return;

            if (e.OldValue is INotifyPropertyChanged oldNotify)
                oldNotify.PropertyChanged -= editor.OnParameterPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newNotify)
                newNotify.PropertyChanged += editor.OnParameterPropertyChanged;

            editor.RefreshSelectedFileLabel();
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(RecordedVoiceParameter.AudioFilePath), StringComparison.Ordinal))
                RefreshSelectedFileLabel();
        }

        private void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            if (Parameter is null)
                return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            var owner = Window.GetWindow(this);
            var window = new RecordedVoiceAudioSelectorWindow(Parameter.RecordsDirectory, Parameter.AudioFilePath)
            {
                Owner = owner
            };

            try
            {
                if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedPath))
                {
                    Parameter.AudioFilePath = window.SelectedPath;
                    RefreshSelectedFileLabel();
                }
            }
            finally
            {
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RefreshSelectedFileLabel()
        {
            var path = Parameter?.AudioFilePath ?? string.Empty;
            SelectedFilePath = path;
            SelectedFileLabel = string.IsNullOrWhiteSpace(path) ? Texts.Unselected : Path.GetFileName(path);
        }
    }
}
