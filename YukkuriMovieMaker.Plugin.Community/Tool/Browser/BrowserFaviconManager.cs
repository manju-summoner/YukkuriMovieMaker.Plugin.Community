using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    static class BrowserFaviconManager
    {
        public static string CacheDirectoryPath { get; } = Path.Combine(AppDirectories.UserDirectory, "resources", "cache", "BrowserFavicon");

        static BrowserFaviconManager()
        {
            if (!Directory.Exists(CacheDirectoryPath))
                Directory.CreateDirectory(CacheDirectoryPath);

            var favorites = BrowserSettings.Default.Favorites;
            foreach (var favorite in favorites)
                favorite.PropertyChanged += OnFavoritePropertyChanged;
            favorites.CollectionChanged += OnFavoritesCollectionChanged;

            CleanupOrphanedIcons();
        }

        public static string GetIconPathForFavorite(string favoriteUrl)
        {
            var key = GetFavoriteKey(favoriteUrl);
            var path = Path.Combine(CacheDirectoryPath, $"{key}.png");
            return File.Exists(path) ? path : string.Empty;
        }

        public static string GetIconPathForHistory(string historyUrl, IEnumerable<BrowserFavorite> favorites)
        {
            var matched = FindMatchingFavorite(historyUrl, favorites);
            return matched is null ? string.Empty : GetIconPathForFavorite(matched.Url);
        }

        public static void SaveIconForFavorite(string currentPageUrl, Stream stream, IEnumerable<BrowserFavorite> favorites)
        {
            var matched = FindMatchingFavorite(currentPageUrl, favorites);
            if (matched is null)
                return;
            var key = GetFavoriteKey(matched.Url);
            var path = Path.Combine(CacheDirectoryPath, $"{key}.png");
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);
        }

        static void OnFavoritesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (BrowserFavorite favorite in e.OldItems)
                    favorite.PropertyChanged -= OnFavoritePropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (BrowserFavorite favorite in e.NewItems)
                    favorite.PropertyChanged += OnFavoritePropertyChanged;
            }
            CleanupOrphanedIcons();
        }

        static void OnFavoritePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrowserFavorite.Url))
                CleanupOrphanedIcons();
        }

        static void CleanupOrphanedIcons()
        {
            var validFileNames = BrowserSettings.Default.Favorites
                .Select(f => GetFavoriteKey(f.Url) + ".png")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(CacheDirectoryPath, "*.png"))
            {
                if (!validFileNames.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            }
        }

        public static bool IsFaviconMissingForMatchingFavorite(string url, IEnumerable<BrowserFavorite> favorites)
        {
            var matched = FindMatchingFavorite(url, favorites);
            if (matched is null)
                return false;
            var key = GetFavoriteKey(matched.Url);
            var path = Path.Combine(CacheDirectoryPath, $"{key}.png");
            return !File.Exists(path);
        }

        static BrowserFavorite? FindMatchingFavorite(string url, IEnumerable<BrowserFavorite> favorites)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            return favorites.FirstOrDefault(f =>
                Uri.TryCreate(f.Url, UriKind.Absolute, out var favUri) &&
                string.Equals(uri.Host, favUri.Host, StringComparison.OrdinalIgnoreCase));
        }

        static string GetFavoriteKey(string favoriteUrl)
        {
            var keySource = Uri.TryCreate(favoriteUrl, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.Host.ToLowerInvariant()
                : favoriteUrl;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(keySource));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
