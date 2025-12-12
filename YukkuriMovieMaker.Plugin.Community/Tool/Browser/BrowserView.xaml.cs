using System.Windows;
using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    /// <summary>
    /// BrowserView.xaml の相互作用ロジック
    /// </summary>
    public partial class BrowserView : UserControl
    {
        public BrowserView()
        {
            InitializeComponent();

            DataContextChanged += BrowserView_DataContextChanged;
        }

        private async void BrowserView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is BrowserViewModel oldVm)
                oldVm.DetachWebView2();
            if (e.NewValue is BrowserViewModel newVm)
            {
                try
                {
                    await webView.EnsureCoreWebView2Async();
                    if (!ReferenceEquals(DataContext, newVm))
                        return;
                    newVm.AttachWebView2(webView);
                }
                catch(Exception ex)
                {
                    newVm.FailToInitializeWebView2(Texts.FailedToInitializeBrowser + "\r\n\r\n" + ex.Message);
                }
            }
        }
    }
}
