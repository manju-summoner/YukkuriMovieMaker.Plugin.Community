using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    public partial class PuppetDeformationMapWindow : Window
    {
        public PuppetDeformationMapWindow()
        {
            InitializeComponent();
            Closed += PuppetDeformationMapWindow_Closed;
        }

        void PuppetDeformationMapWindow_Closed(object? sender, EventArgs e)
        {
            if (DataContext is PuppetDeformationMapViewModel vm)
                vm.Dispose();
        }

        void FitButton_Click(object sender, RoutedEventArgs e)
        {
            MapView.FitToView();
        }
    }
}
