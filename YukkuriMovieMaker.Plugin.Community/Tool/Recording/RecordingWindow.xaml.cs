using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public partial class RecordingWindow : Window
    {
        public RecordingWindow() : this(null)
        {
        }

        public RecordingWindow(string? initialText = null)
        {
            InitializeComponent();
            DataContext = new RecordingWindowViewModel(initialText);
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, System.EventArgs e)
        {
            if (DataContext is System.IDisposable disposable)
                disposable.Dispose();
        }
    }
}




