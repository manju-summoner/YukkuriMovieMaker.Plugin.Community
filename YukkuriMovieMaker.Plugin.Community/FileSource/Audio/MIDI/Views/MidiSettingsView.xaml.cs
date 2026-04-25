using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Views;

public partial class MidiSettingsView
{
    public MidiSettingsView()
    {
        InitializeComponent();
    }

    private void LayerComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (DataContext is MidiSettingsViewModel vm)
            vm.RefreshSoundFontsCommand.Execute(null);
    }
}
