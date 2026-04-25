namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserHistoryItemViewModel(BrowserHistoryEntry entry)
    {
        public string Url { get; } = entry.Url;
        public string Title { get; } = entry.Title;
        public string Display => string.IsNullOrWhiteSpace(Title) ? Url : Title;
        public string IconPath => BrowserFaviconManager.GetIconPathForHistory(Url, BrowserSettings.Default.Favorites);
    }
}
