using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI
{
    /// <summary>
    /// AivisSpeechSettingsView.xaml の相互作用ロジック
    /// </summary>
    public partial class AivisCloudAPISettingsView : UserControl
    {
        public AivisCloudAPISettingsView()
        {
            InitializeComponent();
        }

        private async void AddModelUuidButton_Click(object sender, RoutedEventArgs e)
        {
            var uuidOrUrl = AddModelUuidTextBox.Text;
            var match = UuidRegex().Match(uuidOrUrl);
            if (!match.Success)
            {
                MessageBox.Show(Texts.MissingUuidMessage, Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                AddModelUuidButton.IsEnabled = false;
                AddModelUuidButton.Content = Texts.FetchingModelInfo;

                var uuid = match.Value;

                var info = await API.AivisCloudAPI.GetModelInfoAsync(uuid);
                var existingModel = AivisCloudAPISettings.Default.Models.FirstOrDefault(x => x.AivmModelUuid == uuid);
                if(existingModel != null)
                    AivisCloudAPISettings.Default.Models.Remove(existingModel);
                AivisCloudAPISettings.Default.Models.Add(info);

                AddModelUuidTextBox.Text = string.Empty;
            }
            catch(Exception ex)
            {
                MessageBox.Show($"{Texts.ErrorFetchingModelInfo}\r\n---\r\n{ex.Message}", Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddModelUuidButton.Content = Texts.Add;
                AddModelUuidButton.IsEnabled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var item = (ModelInfoContract)ModelUuidDataGrid.SelectedItem;
            if (item is null)
                return;
            AivisCloudAPISettings.Default.Models.Remove(item);
        }

        [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
        private static partial Regex UuidRegex();
    }
}
