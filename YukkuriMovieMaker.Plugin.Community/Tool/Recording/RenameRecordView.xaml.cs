using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public partial class RenameRecordView : UserControl
    {
        public RenameRecordView()
        {
            InitializeComponent();
            // ホスト Window 側のフォーカス確定が UserControl.Loaded より遅れることがあるため、
            // Dispatcher.Input で1テンポ遅らせて確実に TextBox にフォーカスを移す。
            Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                NameTextBox.SelectAll();
                Keyboard.Focus(NameTextBox);
            }, DispatcherPriority.Input);
        }
    }
}
