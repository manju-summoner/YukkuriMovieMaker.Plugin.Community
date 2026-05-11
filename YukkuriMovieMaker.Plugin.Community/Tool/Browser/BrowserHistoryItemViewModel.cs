using System;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserHistoryItemViewModel
    {
        public string Url { get; }
        public string Title { get; }
        public string Display { get; }
        public string? IconPath { get; }
        public bool IsSelectable { get; }

        public BrowserHistoryItemViewModel(BrowserHistoryEntry entry)
        {
            Url = entry.Url;
            Title = entry.Title;
            Display = string.IsNullOrWhiteSpace(Title) ? Url : Title;
            IconPath = BrowserFaviconManager.GetIconPathForHistory(Url, BrowserSettings.Default.Favorites);
            IsSelectable = true;
        }

        public BrowserHistoryItemViewModel(DateOnly date)
        {
            Url = string.Empty;
            Title = string.Empty;
            Display = date.ToString("yyyy/MM/dd");
            IconPath = null;
            IsSelectable = false;
        }
    }
}
