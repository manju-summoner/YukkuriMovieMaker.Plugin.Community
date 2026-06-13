using System.ComponentModel;
using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

public partial class IrodoriTTSVoiceDesignDialog : Window
{
    public IrodoriTTSVoiceDesignDialog()
    {
        InitializeComponent();
        if (DataContext is IrodoriTTSVoiceDesignDialogViewModel vm)
            vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IrodoriTTSVoiceDesignDialogViewModel.IsSaved)
            && sender is IrodoriTTSVoiceDesignDialogViewModel { IsSaved: true })
        {
            DialogResult = true;
            Close();
        }
    }
}
