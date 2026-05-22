using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    /// <summary>
    /// BrowserView.xaml の相互作用ロジック
    /// </summary>
    public partial class BrowserView : UserControl
    {
        WebView2 webView;

        public BrowserView()
        {
            InitializeComponent();

            webView = CreateWebView2();
            webViewHost.Children.Add(webView);

            DataContextChanged += BrowserView_DataContextChanged;
        }

        private WebView2 CreateWebView2()
        {
            return new WebView2
            {
                CreationProperties = (CoreWebView2CreationProperties)FindResource("WebViewCreationProperties"),
                Style = (Style)FindResource("WebViewStyle"),
            };
        }

        private async void BrowserView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is BrowserViewModel oldVm)
            {
                oldVm.RecreateWebViewRequested -= ViewModel_RecreateWebViewRequested;
                oldVm.DetachWebView2();
            }
            if (e.NewValue is BrowserViewModel newVm)
            {
                newVm.RecreateWebViewRequested += ViewModel_RecreateWebViewRequested;
                await InitializeWebView2Async(newVm);
            }
        }

        private async void ViewModel_RecreateWebViewRequested(object? sender, EventArgs e)
        {
            if (DataContext is not BrowserViewModel vm || !ReferenceEquals(sender, vm))
                return;
            // CoreWebView2.ProcessFailed のコールスタック内でコントロールを破棄すると不安定なため、抜けてから再生成する
            await Task.Yield();
            if (!ReferenceEquals(DataContext, vm))
                return;
            RecreateWebView2Control();
            await InitializeWebView2Async(vm);
        }

        private async Task InitializeWebView2Async(BrowserViewModel vm)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                if (!ReferenceEquals(DataContext, vm))
                    return;
                vm.AttachWebView2(webView);
            }
            catch (Exception ex)
            {
                vm.FailToInitializeWebView2(Texts.FailedToInitializeBrowser + "\r\n\r\n" + ex.Message);
            }
        }

        private void RecreateWebView2Control()
        {
            var oldWebView = webView;
            webViewHost.Children.Remove(oldWebView);
            oldWebView.Dispose();

            webView = CreateWebView2();
            webViewHost.Children.Add(webView);
        }
    }
}
