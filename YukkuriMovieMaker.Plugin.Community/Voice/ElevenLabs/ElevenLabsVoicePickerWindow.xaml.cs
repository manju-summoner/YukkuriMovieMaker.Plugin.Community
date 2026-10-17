using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    public partial class ElevenLabsVoicePickerWindow : Window
    {
        readonly ElevenLabsVoicePickerViewModel viewModel = new();

        public ElevenLabsVoicePickerWindow()
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
