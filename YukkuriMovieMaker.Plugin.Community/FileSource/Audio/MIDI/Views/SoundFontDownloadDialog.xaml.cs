using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Localization;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Views;

public partial class SoundFontDownloadDialog : Window, INotifyPropertyChanged
{
    private double _progressValue;
    private string _message = Texts.Downloading;
    private bool _isIndeterminate = true;

    public double ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(); }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set { _isIndeterminate = value; OnPropertyChanged(); }
    }

    public string TitleText => Texts.PluginName;

    public SoundFontDownloadDialog()
    {
        InitializeComponent();
        WindowThemeService.Bind(this);
        DataContext = this;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new SoundFontDownloadService();
            var progress = new ProgressMessage();
            progress.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(progress.Rate))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (progress.Rate >= 0)
                        {
                            IsIndeterminate = false;
                            ProgressValue = progress.Rate * 100;
                        }
                    });
                }
                else if (ev.PropertyName == nameof(progress.Message))
                {
                    Dispatcher.InvokeAsync(() => Message = progress.Message);
                }
            };
            await svc.DownloadAsync("GeneralUser-GS.sf2.zip", progress);
            DialogResult = true;
        }
        catch
        {
            MessageBox.Show(Texts.DownloadFailed, Texts.PluginName, MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
