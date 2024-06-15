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
using System.Windows.Shapes;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    /// <summary>
    /// PenToolView.xaml の相互作用ロジック
    /// </summary>
    public partial class PenToolView : Window
    {
        public PenToolView()
        {
            InitializeComponent();

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
