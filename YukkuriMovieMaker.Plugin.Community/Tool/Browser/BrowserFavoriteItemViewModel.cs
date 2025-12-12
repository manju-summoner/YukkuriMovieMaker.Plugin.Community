namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserFavoriteItemViewModel(BrowserFavorite favorite) : IBrowserFavoriteItemViewModel
    {
        public string Display => favorite.Name;
        public string[] Path => favorite.Directory.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        public string Url => favorite.Url;
    }
}