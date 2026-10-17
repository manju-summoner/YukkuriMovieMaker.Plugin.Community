using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    /// <summary>
    /// GeminiTTSSettingsView.xaml の相互作用ロジック
    /// </summary>
    public partial class GeminiTTSSettingsView : UserControl
    {
        public GeminiTTSSettingsView()
        {
            InitializeComponent();
        }

        void AddVoicesButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new GeminiTTSVoicePickerWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        void DeleteVoicesButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var voice in voicesDataGrid.SelectedItems.OfType<GeminiTTSVoiceInfo>().ToList())
                GeminiTTSSettings.Default.Voices.Remove(voice);
        }
    }
}
