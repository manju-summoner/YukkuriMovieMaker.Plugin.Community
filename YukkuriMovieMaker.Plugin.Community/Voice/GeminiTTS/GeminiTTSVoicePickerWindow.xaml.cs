using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    public partial class GeminiTTSVoicePickerWindow : Window
    {
        readonly GeminiTTSVoicePickerViewModel viewModel = new();

        public GeminiTTSVoicePickerWindow()
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (_, _) => await viewModel.LoadAsync();
        }

        void AddButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Apply();
            DialogResult = true;
        }
    }
}
