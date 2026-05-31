using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

public partial class PiperPlusSettingsView : UserControl
{
    public PiperPlusSettingsView()
    {
        InitializeComponent();
        DataContext = new PiperPlusSettingsViewModel();
    }
}
