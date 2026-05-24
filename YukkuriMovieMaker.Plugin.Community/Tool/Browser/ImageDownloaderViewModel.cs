using Microsoft.Web.WebView2.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class ImageDownloaderViewModel : Bindable
    {
        const string CollectImagesScript = """
            (function() {
                const urls = new Set();

                const addUrl = (raw) => {
                    if (!raw || typeof raw !== 'string') return;
                    const trimmed = raw.trim();
                    if (!trimmed) return;
                    try {
                        if (trimmed.startsWith('data:image/')) {
                            urls.add(trimmed);
                            return;
                        }
                        const abs = new URL(trimmed, location.href).href;
                        if (abs.startsWith('http://') || abs.startsWith('https://'))
                            urls.add(abs);
                    } catch {}
                };

                const addSrcset = (srcset) => {
                    if (!srcset) return;
                    srcset.split(',').forEach(entry => {
                        const url = entry.trim().split(/\s+/)[0];
                        addUrl(url);
                    });
                };

                const lazyAttrs = [
                    'src', 'srcset', 'data-src', 'data-srcset', 'data-original',
                    'data-original-src', 'data-lazy', 'data-lazy-src', 'data-url',
                    'data-img', 'data-image', 'data-load', 'data-echo',
                    'data-bg', 'data-background', 'data-defer-src',
                ];

                const collectFromElement = (el) => {
                    if (el.tagName === 'IMG') {
                        addUrl(el.currentSrc || el.src);
                        addSrcset(el.srcset);
                    }
                    if (el.tagName === 'SOURCE') {
                        addSrcset(el.srcset);
                        addUrl(el.src);
                    }
                    if (el.tagName === 'VIDEO') {
                        addUrl(el.poster);
                    }
                    if (el.tagName === 'CANVAS') {
                        try { addUrl(el.toDataURL()); } catch {}
                    }
                    lazyAttrs.forEach(attr => {
                        const val = el.getAttribute(attr);
                        if (!val) return;
                        if (attr.endsWith('set')) addSrcset(val);
                        else addUrl(val);
                    });
                    const style = el.getAttribute('style') || '';
                    if (style) extractCssUrls(style);
                    const computed = window.getComputedStyle(el);
                    extractCssUrls(computed.backgroundImage);
                };

                const extractCssUrls = (cssText) => {
                    if (!cssText || cssText === 'none') return;
                    const re = /url\(\s*["']?([^"')]+)["']?\s*\)/g;
                    let m;
                    while ((m = re.exec(cssText)) !== null) addUrl(m[1]);
                };

                const walkNode = (root) => {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
                    let node = walker.currentNode;
                    while (node) {
                        collectFromElement(node);
                        if (node.shadowRoot) walkNode(node.shadowRoot);
                        node = walker.nextNode();
                    }
                };

                walkNode(document.documentElement);

                try {
                    for (const sheet of document.styleSheets) {
                        try {
                            for (const rule of sheet.cssRules) {
                                if (rule.style) extractCssUrls(rule.style.cssText);
                                if (rule.cssText) extractCssUrls(rule.cssText);
                            }
                        } catch {}
                    }
                } catch {}

                document.querySelectorAll('meta[property="og:image"], meta[property="og:image:url"]')
                    .forEach(el => addUrl(el.content));
                document.querySelectorAll('meta[name="twitter:image"], meta[name="twitter:image:src"]')
                    .forEach(el => addUrl(el.content));
                document.querySelectorAll('link[rel="preload"][as="image"]')
                    .forEach(el => addUrl(el.href));
                document.querySelectorAll('link[rel="image_src"]')
                    .forEach(el => addUrl(el.href));

                return JSON.stringify([...urls]);
            })()
            """;

        readonly CoreWebView2 core;
        readonly Action onClose;
        CancellationTokenSource? loadCts;
        CancellationTokenSource? downloadCts;

        public ObservableCollection<ImageDownloaderItemViewModel> Items { get; } = [];
        public bool IsLoading { get => field; private set => Set(ref field, value); }
        public bool IsDownloading { get => field; private set => Set(ref field, value); }
        public int DownloadProgress { get => field; private set => Set(ref field, value); }
        public int DownloadTotal { get => field; private set => Set(ref field, value); }

        public string SaveFolder
        {
            get => BrowserSettings.Default.ImageDownloadFolder;
            set
            {
                if (BrowserSettings.Default.ImageDownloadFolder == value)
                    return;
                BrowserSettings.Default.ImageDownloadFolder = value;
                OnPropertyChanged(nameof(SaveFolder));
                DownloadSelectedCommand.RaiseCanExecuteChanged();
            }
        }

        public ActionCommand SelectAllCommand { get; }
        public ActionCommand DeselectAllCommand { get; }
        public ActionCommand RefreshCommand { get; }
        public ActionCommand SelectSaveFolderCommand { get; }
        public ActionCommand DownloadSelectedCommand { get; }

        public ImageDownloaderViewModel(CoreWebView2 core, Action onClose)
        {
            this.core = core;
            this.onClose = onClose;

            SelectAllCommand = new ActionCommand(
                _ => Items.Count > 0,
                _ => SetAllSelected(true));
            DeselectAllCommand = new ActionCommand(
                _ => Items.Count > 0,
                _ => SetAllSelected(false));
            RefreshCommand = new ActionCommand(
                _ => !IsLoading,
                _ => LoadImagesAsync());
            SelectSaveFolderCommand = new ActionCommand(
                _ => true,
                _ => SelectSaveFolder());
            DownloadSelectedCommand = new ActionCommand(
                _ => !IsDownloading && !string.IsNullOrWhiteSpace(SaveFolder) && Items.Any(x => x.IsSelected),
                _ => DownloadSelectedAsync());

            LoadImagesAsync();
        }

        void SetAllSelected(bool selected)
        {
            foreach (var item in Items)
                item.IsSelected = selected;
        }

        void SelectSaveFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog()
            {
                Title = Texts.SelectSaveFolder,
                InitialDirectory = Directory.Exists(SaveFolder) ? SaveFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            if (dialog.ShowDialog() == true)
                SaveFolder = dialog.FolderName;
        }

        static string NormalizeUrl(string url)
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url;
            var path = uri.AbsolutePath.TrimEnd('/');
            return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}{path}";
        }

        async void LoadImagesAsync()
        {
            loadCts?.Cancel();
            loadCts = new CancellationTokenSource();
            var token = loadCts.Token;

            IsLoading = true;
            Items.Clear();
            RefreshCommand.RaiseCanExecuteChanged();

            try
            {
                var rawJson = await core.ExecuteScriptAsync(CollectImagesScript);
                if (token.IsCancellationRequested)
                    return;

                var innerJson = JsonSerializer.Deserialize<string>(rawJson) ?? "[]";
                var urls = JsonSerializer.Deserialize<string[]>(innerJson) ?? [];
                var items = urls
                    .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _)
                             || u.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(NormalizeUrl, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Select(u => new ImageDownloaderItemViewModel(u))
                    .ToList();

                foreach (var item in items)
                    Items.Add(item);

                SelectAllCommand.RaiseCanExecuteChanged();
                DeselectAllCommand.RaiseCanExecuteChanged();
                DownloadSelectedCommand.RaiseCanExecuteChanged();

                await Task.WhenAll(items.Select(async item =>
                {
                    var valid = await item.TryLoadThumbnailAsync(token);
                    if (!valid && !token.IsCancellationRequested)
                        Application.Current?.Dispatcher?.Invoke(() => Items.Remove(item));
                }));
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoading = false;
                    RefreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        async void DownloadSelectedAsync()
        {
            var targets = Items.Where(x => x.IsSelected).ToList();
            if (targets.Count == 0 || string.IsNullOrWhiteSpace(SaveFolder))
                return;

            downloadCts?.Cancel();
            downloadCts = new CancellationTokenSource();
            var token = downloadCts.Token;

            try
            {
                Directory.CreateDirectory(SaveFolder);
            }
            catch
            {
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadTotal = targets.Count;
            DownloadSelectedCommand.RaiseCanExecuteChanged();

            try
            {
                var semaphore = new SemaphoreSlim(4, 4);
                var tasks = targets.Select(async item =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        await item.SaveToAsync(SaveFolder, token);
                    }
                    finally
                    {
                        semaphore.Release();
                        Application.Current?.Dispatcher?.Invoke(() => DownloadProgress++);
                    }
                });
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsDownloading = false;
                DownloadSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
