using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal partial class BrowserViewModel : Bindable, IToolViewModel, IDisposable
    {
        public static string UserDataFolderPath { get; } = Path.Combine(AppDirectories.UserDirectory, "WebView2");
        public static string ExtensionsFolderPath { get; } = Path.Combine(UserDataFolderPath, "Extensions");

        TaskCompletionSource<WebView2>? webView2TCS;
        WebView2? webView2;
        public bool IsLoading { get => field; private set => Set(ref field, value); }
        public double LoadingProgress { get => field; private set => Set(ref field, value); }
        CancellationTokenSource? progressCts;
        WebBrowserSavedState state = new("google.com", 1d);
        static bool isExtensionsLoaded;

        const string MobileUserAgent = "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36";
        static string? defaultUserAgent;

        public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

        public bool IsFailedToInitializeWebView2 { get; private set; }
        public string FailedToInitializeWebView2Message { get; private set; } = string.Empty;

        public ObservableCollection<BrowserHistoryItemViewModel> History { get; } = [];
        public bool IsMobileMode
        {
            get => field;
            set
            {
                if (Set(ref field, value) && webView2?.CoreWebView2 != null)
                {
                    webView2.CoreWebView2.Settings.UserAgent = value ? MobileUserAgent : (defaultUserAgent ?? string.Empty);
                    webView2.CoreWebView2.Reload();
                }
            }
        }

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
        public ActionCommand ClearBrowsingDataCommand { get; }
        public ActionCommand OpenBrowserSettingsCommand { get; }
        public ActionCommand DownloadCommand { get; }
        public ActionCommand PrintCommand { get; }
        public ActionCommand FindCommand { get; }

        public BrowserFavoriteEditorViewModel? FavoriteEditorViewModel { get; set=>Set(ref field, value); }
        public ClearBrowsingDataViewModel? ClearBrowsingDataViewModel { get => field; set => Set(ref field, value); }
        public BrowserSettingsViewModel? BrowserSettingsViewModel { get => field; set => Set(ref field, value); }

        [SuppressMessage("Performance", "CA1822:メンバーを static に設定します", Justification = "")]
        public BrowserFavoriteDirectoryViewModel FavoriteDirectoryViewModel => BrowserFavoriteDirectoryViewModel.CreateBrowserFavoriteRoot();

        public BrowserViewModel()
        {
            DateOnly? lastDate = null;
            foreach (var entry in BrowserHistoryManager.LoadHistory())
            {
                var localDate = DateOnly.FromDateTime(entry.Timestamp.ToLocalTime().DateTime);
                if (lastDate != localDate)
                {
                    History.Add(new BrowserHistoryItemViewModel(localDate));
                    lastDate = localDate;
                }
                History.Add(new BrowserHistoryItemViewModel(entry));
            }
            CreateNewWindowCommand = new ActionCommand(
                _ => true,
                _ => ExecuteCreateNewWindow());
            GoBackCommand = new ActionCommand(
                _ => webView2?.CoreWebView2?.CanGoBack ?? false,
                _ => webView2?.CoreWebView2?.GoBack());
            GoForwardCommand = new ActionCommand(
                _ => webView2?.CoreWebView2?.CanGoForward ?? false,
                _ => webView2?.CoreWebView2?.GoForward());
            RefreshCommand = new ActionCommand(
                _ => webView2 != null && !IsLoading,
                _ => webView2?.CoreWebView2?.Reload());
            StopCommand = new ActionCommand(
                _ => webView2 != null && IsLoading,
                _ => webView2?.CoreWebView2?.Stop());
            NavigateCommand = new ActionCommand(
                _ => webView2 != null,
                parameter => ExecuteNavigate(parameter));
            OpenFavoriteEditorCommand = new ActionCommand(
                _ => true,
                parameter => ExecuteOpenFavoriteEditor(parameter));
            ClearBrowsingDataCommand = new ActionCommand(
                _ => webView2?.CoreWebView2?.Profile != null,
                async _ => await ExecuteClearBrowsingDataAsync());
            OpenBrowserSettingsCommand = new ActionCommand(
                _ => true,
                _ => ExecuteOpenBrowserSettings());
            DownloadCommand = new ActionCommand(
                _ => webView2?.CoreWebView2 != null,
                _ => webView2?.CoreWebView2?.OpenDefaultDownloadDialog());
            PrintCommand = new ActionCommand(
                _ => webView2?.CoreWebView2 != null,
                _ => webView2?.CoreWebView2?.ShowPrintUI());
            FindCommand = new ActionCommand(
                _ => webView2?.CoreWebView2 != null,
                async _ => await ExecuteFindAsync());
        }

        private void ExecuteCreateNewWindow()
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
        }

        private void ExecuteNavigate(object? parameter)
        {
            var url = NormalizeOrCreateSearchUrl(parameter as string ?? string.Empty);
            if (NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty) == url)
                return;
            webView2?.CoreWebView2?.Navigate(url);
        }

        private void ExecuteOpenFavoriteEditor(object? parameter)
        {
            if (parameter is not string location)
                return;
            var favorite = BrowserSettings.Default.Favorites.FirstOrDefault(f => f.Url == location);
            if (favorite is null)
            {
                favorite = new BrowserFavorite()
                {
                    Url = location,
                    Name = webView2?.CoreWebView2.DocumentTitle ?? location,
                    Directory = string.Empty,
                };
                BrowserSettings.Default.Favorites.Add(favorite);
                _ = FetchAndSaveFaviconAsync(location);
            }
            FavoriteEditorViewModel = new BrowserFavoriteEditorViewModel(favorite);
            FavoriteEditorViewModel = null;
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
        }

        private void ExecuteOpenBrowserSettings()
        {
            BrowserSettingsViewModel = new BrowserSettingsViewModel();
            BrowserSettingsViewModel = null;
        }

        private Task ExecuteClearBrowsingDataAsync()
        {
            if (webView2?.CoreWebView2?.Profile == null)
                return Task.CompletedTask;

            ClearBrowsingDataViewModel = new ClearBrowsingDataViewModel(async vm =>
            {
                if (vm.ClearBrowserCache && webView2?.CoreWebView2?.Profile != null)
                {
                    await webView2.CoreWebView2.Profile.ClearBrowsingDataAsync();
                }
                if (vm.ClearPluginHistory)
                {
                    BrowserHistoryManager.ClearHistory();
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => History.Clear());
                }
                if (vm.ClearPluginFavicon)
                {
                    BrowserFaviconManager.ClearFavicons();
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                }
                ClearBrowsingDataViewModel = null;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    System.Windows.MessageBox.Show(
                        Texts.ClearBrowsingDataCompletedMessage,
                        Texts.ClearBrowsingData,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information));
            });

            return Task.CompletedTask;
        }

        private async Task ExecuteFindAsync()
        {
            var coreWebView2 = webView2?.CoreWebView2;
            if (coreWebView2?.Find == null || coreWebView2.Environment == null)
                return;

            try
            {
                var options = coreWebView2.Environment.CreateFindOptions();

                options.FindTerm = "";
                options.IsCaseSensitive = false;
                options.ShouldHighlightAllMatches = true;
                options.SuppressDefaultFindDialog = false;

                await coreWebView2.Find.StartAsync(options);
            }
            catch
            {
            }
        }

        public void AttachWebView2(WebView2 webView2Service)
        {
            webView2 = webView2Service;
            defaultUserAgent ??= webView2.CoreWebView2.Settings.UserAgent;
            webView2.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView2.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView2.CoreWebView2.ContentLoading += CoreWebView2_ContentLoading;
            webView2.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            webView2.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            webView2.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
            webView2.CoreWebView2.FaviconChanged += CoreWebView2_FaviconChanged;

            BrowserSettings.Default.PropertyChanged += BrowserSettings_PropertyChanged;

            ApplySettings();
            ApplyState();

            _ = LoadExtensionsAsync();

            webView2TCS?.SetResult(webView2Service);
            webView2TCS = null;
        }

        public void DetachWebView2()
        {
            webView2?.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            webView2?.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
            if (webView2 != null)
            {
                webView2.CoreWebView2.ContentLoading -= CoreWebView2_ContentLoading;
                webView2.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
            }
            webView2?.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            webView2?.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
            webView2?.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
            if (webView2 != null)
            {
                webView2.CoreWebView2.FaviconChanged -= CoreWebView2_FaviconChanged;
            }

            BrowserSettings.Default.PropertyChanged -= BrowserSettings_PropertyChanged;

            webView2?.CoreWebView2.Navigate("about:blank");

            webView2 = null;
        }

        private async Task LoadExtensionsAsync()
        {
            if (isExtensionsLoaded || webView2?.CoreWebView2?.Profile is not { } profile)
                return;

            isExtensionsLoaded = true;

            if (!Directory.Exists(ExtensionsFolderPath))
            {
                Directory.CreateDirectory(ExtensionsFolderPath);
                return;
            }

            foreach (var extensionPath in Directory.EnumerateDirectories(ExtensionsFolderPath))
            {
                try
                {
                    await profile.AddBrowserExtensionAsync(extensionPath);
                }
                catch
                {
                }
            }
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
            IsLoading = true;
            LoadingProgress = 0;
            Location = NormalizeUrl(e.Uri);
            UpdateCommandCanExecuteState();

            progressCts?.Cancel();
            progressCts = new CancellationTokenSource();
            _ = SimulateProgressAsync(progressCts.Token);
        }

        private async Task SimulateProgressAsync(CancellationToken token)
        {
            try
            {
                LoadingProgress = 10;
                while (!token.IsCancellationRequested && LoadingProgress < 85)
                {
                    await Task.Delay(200, token);
                    var remaining = 85 - LoadingProgress;
                    LoadingProgress += remaining * 0.1;
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CoreWebView2_ContentLoading(object? sender, CoreWebView2ContentLoadingEventArgs e)
        {
            if (LoadingProgress < 40) LoadingProgress = 40;
        }

        private void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            if (LoadingProgress < 70) LoadingProgress = 70;
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            progressCts?.Cancel();
            LoadingProgress = 100;
            try
            {
                await Task.Delay(200);
            }
            catch { }

            IsLoading = false;
            LoadingProgress = 0;
            UpdateCommandCanExecuteState();
            if (webView2?.CoreWebView2 is null)
                return;
            AddHistory(webView2.CoreWebView2.Source, webView2.CoreWebView2.DocumentTitle);
            var url = webView2.CoreWebView2.Source;
            var favorites = BrowserSettings.Default.Favorites;
            if (!BrowserFaviconManager.IsFaviconMissingForMatchingFavorite(url, favorites))
                return;
            try
            {
                using var stream = await webView2.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
                if (stream != null && stream.Length > 0)
                {
                    BrowserFaviconManager.SaveIconForFavorite(url, stream, favorites);
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                }
            }
            catch { }
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            Location = NormalizeUrl(webView2?.CoreWebView2.Source ?? string.Empty);
            OnPropertyChanged(nameof(IsFavorite));
            UpdateCommandCanExecuteState();
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            Title = webView2?.CoreWebView2.DocumentTitle ?? Texts.Browser;
        }

        private async void CoreWebView2_FaviconChanged(object? sender, object e)
        {
            if (webView2?.CoreWebView2 is null || string.IsNullOrEmpty(webView2.CoreWebView2.FaviconUri)) return;
            var url = webView2.CoreWebView2.Source;
            var favorites = BrowserSettings.Default.Favorites;
            if (!favorites.Any(f => Uri.TryCreate(f.Url, UriKind.Absolute, out var fUri) && Uri.TryCreate(url, UriKind.Absolute, out var uUri) && string.Equals(fUri.Host, uUri.Host, StringComparison.OrdinalIgnoreCase)))
                return;
            try
            {
                using var stream = await webView2.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
                if (stream != null)
                {
                    BrowserFaviconManager.SaveIconForFavorite(url, stream, favorites);
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                }
            }
            catch { }
        }

        private void AddHistory(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;

            BrowserHistoryManager.AddEntry(url, title);

            History.Clear();
            DateOnly? lastDate = null;
            foreach (var entry in BrowserHistoryManager.LoadHistory())
            {
                var localDate = DateOnly.FromDateTime(entry.Timestamp.ToLocalTime().DateTime);
                if (lastDate != localDate)
                {
                    History.Add(new BrowserHistoryItemViewModel(localDate));
                    lastDate = localDate;
                }
                History.Add(new BrowserHistoryItemViewModel(entry));
            }
        }

        private async Task FetchAndSaveFaviconAsync(string url)
        {
            var coreWebView2 = webView2?.CoreWebView2;
            if (coreWebView2 is null || string.IsNullOrEmpty(coreWebView2.FaviconUri))
                return;
            var favorites = BrowserSettings.Default.Favorites;
            if (!BrowserFaviconManager.IsFaviconMissingForMatchingFavorite(url, favorites))
                return;
            try
            {
                using var stream = await coreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
                if (stream != null && stream.Length > 0)
                {
                    BrowserFaviconManager.SaveIconForFavorite(url, stream, favorites);
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                }
            }
            catch { }
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
            Location = state.Location;
            Title = stateData.Title ?? Texts.Browser;

            if (webView2 != null)
                ApplyState();
        }

        private void ApplyState()
        {
            var url = NormalizeOrCreateSearchUrl(state.Location);
            webView2?.CoreWebView2.Navigate(url);
            webView2?.ZoomFactor = state.Zoom;
        }

        private void BrowserSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (webView2?.CoreWebView2 == null) return;
            var settings = webView2.CoreWebView2.Settings;
            settings.IsReputationCheckingRequired = BrowserSettings.Default.IsSmartScreenEnabled;
            settings.IsPasswordAutosaveEnabled = BrowserSettings.Default.IsPasswordAutosaveEnabled;
            settings.IsGeneralAutofillEnabled = BrowserSettings.Default.IsGeneralAutofillEnabled;
            settings.IsScriptEnabled = BrowserSettings.Default.IsScriptEnabled;
            
            if (webView2.CoreWebView2.Profile != null)
            {
                webView2.CoreWebView2.Profile.PreferredTrackingPreventionLevel = (CoreWebView2TrackingPreventionLevel)(BrowserSettings.Default.TrackingPreventionLevel + 1);
            }

            string userAgent = defaultUserAgent ?? string.Empty;
            if (IsMobileMode)
                userAgent = MobileUserAgent;
            else if (!string.IsNullOrEmpty(BrowserSettings.Default.CustomUserAgent))
                userAgent = BrowserSettings.Default.CustomUserAgent;

            if (settings.UserAgent != userAgent)
            {
                settings.UserAgent = userAgent;
                webView2.CoreWebView2.Reload();
            }
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

        static string NormalizeOrCreateSearchUrl(string urlOrQuery)
        {
            if (Uri.TryCreate(urlOrQuery, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }
            else if (MaybeUrlWithoutScheme(urlOrQuery) && Uri.TryCreate("https://" + urlOrQuery, UriKind.Absolute, out uri))
            {
                return uri.ToString();
            }
            else
            {
                return "https://www.google.com/search?q=" + Uri.EscapeDataString(urlOrQuery);
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

        void UpdateCommandCanExecuteState()
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

        public void Dispose()
        {
            DetachWebView2();
        }
    }
}