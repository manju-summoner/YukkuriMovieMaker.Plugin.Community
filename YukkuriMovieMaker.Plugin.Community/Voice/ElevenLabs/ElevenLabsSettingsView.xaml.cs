using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    /// <summary>
    /// ElevenLabsSettingsView.xaml の相互作用ロジック
    /// </summary>
    public partial class ElevenLabsSettingsView : UserControl
    {
        public ElevenLabsSettingsView()
        {
            InitializeComponent();
        }

        void AddFromElevenLabsButton_Click(object sender, RoutedEventArgs e)
        {
            //入力中の行があると、選択画面が表に足す行と DataGrid の新しい行の追加が重なるので、先に確定しておく
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var window = new ElevenLabsVoicePickerWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
    }
}
