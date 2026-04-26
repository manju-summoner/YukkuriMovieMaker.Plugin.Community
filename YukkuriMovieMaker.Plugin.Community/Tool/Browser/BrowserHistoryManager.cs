using System.IO;
using System.Text.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    static class BrowserHistoryManager
    {
        const int MaxHistoryCount = 128;

        public static string CacheDirectoryPath { get; } =
            Path.Combine(AppDirectories.UserDirectory, "resources", "cache", "BrowserHistory");

        static BrowserHistoryManager()
        {
            if (!Directory.Exists(CacheDirectoryPath))
                Directory.CreateDirectory(CacheDirectoryPath);
        }

        public static void AddEntry(string url, string title)
        {
            var now = DateTimeOffset.UtcNow;
            var localDate = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
            var localDateEntries = LoadDayEntries(localDate);

            if (localDateEntries.Count > 0 && localDateEntries[0].Url == url)
                return;

            localDateEntries.Insert(0, new BrowserHistoryEntry(url, title, now));
            SaveDayEntries(localDate, localDateEntries);
            TrimToMaxCount();
        }

        public static IReadOnlyList<BrowserHistoryEntry> LoadHistory()
        {
            var result = new List<BrowserHistoryEntry>();
            foreach (var file in GetDayFilesDescending())
            {
                if (result.Count >= MaxHistoryCount)
                    break;
                var date = ParseDateFromFileName(file);
                if (date is null)
                    continue;
                result.AddRange(LoadDayEntries(date.Value));
            }
            return result.OrderByDescending(x => x.Timestamp).Take(MaxHistoryCount).ToList();
        }

        static void TrimToMaxCount()
        {
            var files = GetDayFilesDescending().ToList();
            int total = 0;
            foreach (var file in files)
            {
                var date = ParseDateFromFileName(file);
                if (date is null)
                {
                    File.Delete(file);
                    continue;
                }
                if (total >= MaxHistoryCount)
                {
                    File.Delete(file);
                    continue;
                }
                var entries = LoadDayEntries(date.Value);
                int remaining = MaxHistoryCount - total;
                if (entries.Count > remaining)
                {
                    SaveDayEntries(date.Value, entries.Take(remaining).ToList());
                    total = MaxHistoryCount;
                }
                else
                {
                    total += entries.Count;
                }
            }
        }

        static IEnumerable<string> GetDayFilesDescending() =>
            Directory.EnumerateFiles(CacheDirectoryPath, "????-??-??.json")
                .OrderByDescending(f => f);

        static DateOnly? ParseDateFromFileName(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            return DateOnly.TryParseExact(name, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date)
                ? date
                : null;
        }

        static List<BrowserHistoryEntry> LoadDayEntries(DateOnly date)
        {
            var path = GetDayFilePath(date);
            if (!File.Exists(path))
                return [];
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<BrowserHistoryEntry>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        static void SaveDayEntries(DateOnly date, List<BrowserHistoryEntry> entries)
        {
            var path = GetDayFilePath(date);
            if (entries.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonSerializer.Serialize(entries));
        }

        static string GetDayFilePath(DateOnly date) =>
            Path.Combine(CacheDirectoryPath, $"{date:yyyy-MM-dd}.json");

        public static void ClearHistory()
        {
            if (!Directory.Exists(CacheDirectoryPath))
                return;
            foreach (var file in Directory.EnumerateFiles(CacheDirectoryPath, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
    }
}
