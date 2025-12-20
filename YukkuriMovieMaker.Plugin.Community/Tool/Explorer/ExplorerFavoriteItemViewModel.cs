namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    class ExplorerFavoriteItemViewModel(ExplorerFavorite favorite) : IExplorerFavoriteItemViewModel
    {
        public string Display => favorite.Name;
        public string[] Path => favorite.Directory.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        public string Url => favorite.Url;
    }
}