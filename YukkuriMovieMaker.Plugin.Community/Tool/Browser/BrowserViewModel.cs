using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal partial class BrowserViewModel : Bindable, IToolViewModel
    {
        TaskCompletionSource<WebView2>? webView2TCS;
        WebView2? webView2;
        bool isLoading;
        WebBrowserSavedState state = new("google.com", 1d);

        public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

        public bool IsFailedToInitializeWebView2 { get; private set; }
        public string FailedToInitializeWebView2Message { get; private set; } = string.Empty;

        public string Title { get; private set => Set(ref field, value); } = Texts.Browser;
        public string Location { get; private set => Set(ref field, value, nameof(Location), nameof(IsFavorite)); } = string.Empty;

        public bool IsFavorite => BrowserSettings.Default.Favorites.Any(x => x.Url == Location);
        public bool IsMenuOpened { get; set => Set(ref field, value); } = false;
        public ActionCommand CreateNewWindowCommand { get; }
        public ActionCommand GoBackCommand { get; }
        public ActionCommand GoForwardCommand { get; }
        public ActionCommand RefreshCommand { get; }
        public ActionCommand StopCommand { get; }
        public ActionCommand NavigateCommand { get; }
        public ActionCommand OpenFavoriteEditorCommand { get; }

        public BrowserFavoriteEditorViewModel? FavoriteEditorViewModel { get; set=>Set(ref field, value); }

        [SuppressMessage("Performance", "CA1822:メンバーを static に設定します", Justification = "")]
        public BrowserFavoriteDirectoryViewModel FavoriteDirectoryViewModel => BrowserFavoriteDirectoryViewModel.CreateBrowserFavoriteRoot();

        public BrowserViewModel() 
        {
            CreateNewWindowCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var toolState = new ToolState()
                    {
                        SavedState = Json.Json.GetJsonText(new WebBrowserSavedState(
                                Location: Location,
                                Zoom: webView2?.ZoomFactor ?? 1.0
                            )),
                    };
                    var args = new CreateNewToolViewRequestedEventArgs(toolState);
                    CreateNewToolViewRequested?.Invoke(this, args);
                });
            GoBackCommand = new ActionCommand(
                _ => webView2?.CoreWebView2?.CanGoBack ?? false,
                _ => webView2?.CoreWebView2?.GoBack());
            GoForwardCommand = new ActionCommand(
                _ => webView2?.CoreWebView2?.CanGoForward ?? false,
                _ => webView2?.CoreWebView2?.GoForward());
            RefreshCommand = new ActionCommand(
                _ => webView2 != null && !isLoading,
                _ => webView2?.CoreWebView2?.Reload());
            StopCommand = new ActionCommand(
                _ => webView2 != null && isLoading,
                _ => webView2?.CoreWebView2?.Stop());
            NavigateCommand = new ActionCommand(
                _ => webView2 != null,
                x =>
                {
                    var url = NormalizeOrCreateSearchUrl(x as string ?? string.Empty);
                    if (NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty) == url)
                        return;
                    webView2?.CoreWebView2?.Navigate(url);
                });
            OpenFavoriteEditorCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is not string location)
                        return;
                    var favorite = BrowserSettings.Default.Favorites.FirstOrDefault(x => x.Url == location);
                    if(favorite is null)
                    {
                        favorite = new BrowserFavorite()
                        {
                            Url = location,
                            Name = webView2?.CoreWebView2.DocumentTitle ?? location,
                            Directory = string.Empty,
                        };
                        BrowserSettings.Default.Favorites.Add(favorite);
                    }
                    FavoriteEditorViewModel = new BrowserFavoriteEditorViewModel(favorite);
                    FavoriteEditorViewModel = null;
                    OnPropertyChanged(nameof(IsFavorite));
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                });
        }

        public void AttachWebView2(WebView2 webView2Service)
        {
            webView2 = webView2Service;
            webView2.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView2.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            webView2.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            webView2.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;

            ApplyState();

            webView2TCS?.SetResult(webView2Service);
            webView2TCS = null;
        }

        public void DetachWebView2()
        {
            webView2?.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            webView2?.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
            webView2?.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            webView2?.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
            webView2?.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;

            webView2 = null;
        }

        private async void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                var toolState = new ToolState() 
                {
                    SavedState = Json.Json.GetJsonText(new WebBrowserSavedState(
                        Location: e.Uri,
                        Zoom: webView2?.ZoomFactor ?? 1.0
                        )),
                };
                var args = new CreateNewToolViewRequestedEventArgs(toolState);
                CreateNewToolViewRequested?.Invoke(this, args);
                if (args.ResultNewToolViewModel is BrowserViewModel newBrowserVM)
                {
                    var webView2 = await newBrowserVM.GetWebView2Async();
                    e.NewWindow = webView2.CoreWebView2;
                }
            }
            finally
            {
                deferral.Complete();
                e.Handled = true;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            isLoading = true;
            Location = NormalizeUrl(e.Uri);
            RefreshCommandCanExecutions();
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            isLoading = false;
            RefreshCommandCanExecutions();
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            Location = NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty);
            OnPropertyChanged(nameof(IsFavorite));
            RefreshCommandCanExecutions();
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            Title = webView2?.CoreWebView2.DocumentTitle ?? Texts.Browser;
        }
        public void FailToInitializeWebView2(string message)
        {
            IsFailedToInitializeWebView2 = true;
            FailedToInitializeWebView2Message = message;

            webView2TCS?.SetException(new InvalidOperationException(message));
            webView2TCS = null;
        }
        public async Task<WebView2> GetWebView2Async()
        {
            if (webView2 != null)
                return webView2;
            webView2TCS ??= new TaskCompletionSource<WebView2>(TaskCreationOptions.RunContinuationsAsynchronously);
            return await webView2TCS.Task;
        }


        public void LoadState(ToolState stateData)
        {
            if (stateData.SavedState is null)
                return;
            var savedState = Json.Json.LoadFromText<WebBrowserSavedState>(stateData.SavedState);
            if (savedState is null)
                return;
            state = savedState;
            if (webView2 != null)
                ApplyState();
        }

        private void ApplyState()
        {
            var url = NormalizeOrCreateSearchUrl(state.Location);
            if (NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty) != url)
                webView2?.CoreWebView2.Navigate(url);
            webView2?.ZoomFactor = state.Zoom;

            Title = webView2?.CoreWebView2.DocumentTitle ?? Texts.Browser;
            Location = NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty);
        }

        public ToolState SaveState()
        {
            return new ToolState
            {
                Title = Title,
                SavedState = Json.Json.GetJsonText(new WebBrowserSavedState(
                    Location: this.Location,
                    Zoom: webView2?.ZoomFactor ?? 1.0
                    )),
            };
        }

        static string NormalizeOrCreateSearchUrl(string v)
        {
            if (Uri.TryCreate(v, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }
            else if (MaybeUrlWithoutScheme(v) && Uri.TryCreate("https://" + v, UriKind.Absolute, out uri))
            {
                return uri.ToString();
            }
            else
            {
                return "https://www.google.com/search?q=" + Uri.EscapeDataString(v);
            }
        }
        static string NormalizeUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }
            else
            {
                return url;
            }
        }
        static bool MaybeUrlWithoutScheme(string url)
        {
            var schemes = new[]
            {
                "http://",
                "https://",
                "ftp://",
                "ftps://",
                "file://",
                "about:",
                "data:",
                "mailto:",
                "news:",
                "nntp:",
                "tel:",
                "telnet:",
                "ws://",
                "wss://",
            };
            if (schemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
                return false;

            var parts = url.Split('/');
            if (parts.Length is 0)
                return false;
            var hostPart = parts[0];

            if (LocalhostOrIPAddressWithPortRegex().IsMatch(hostPart))
                return true;

            if (TopLevelDomain.All.Any(tld => hostPart.EndsWith("." + tld, StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        void RefreshCommandCanExecutions()
        {
            var commands = new ActionCommand[]
            {
                GoBackCommand,
                GoForwardCommand,
                RefreshCommand,
                StopCommand,
            };
            foreach (var cmd in commands)
                cmd.RaiseCanExecuteChanged();
        }

        [GeneratedRegex(@"^(localhost|[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})(:[0-9]+)?$")]
        private static partial Regex LocalhostOrIPAddressWithPortRegex();
    }
}