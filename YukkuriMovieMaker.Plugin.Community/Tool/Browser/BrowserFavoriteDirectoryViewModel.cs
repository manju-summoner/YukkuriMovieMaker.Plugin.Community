namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserFavoriteDirectoryViewModel : IBrowserFavoriteItemViewModel
    {
        public string Display { get; }
        public string[] Path { get; }
        public IBrowserFavoriteItemViewModel[] Items { get; }

        public BrowserFavoriteDirectoryViewModel(string path, BrowserFavoriteItemViewModel[] favorites)
        {
            Path = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            Display = Path.Length is 0 ? string.Empty : Path[^1];

            var items = new List<IBrowserFavoriteItemViewModel>();
            //フォルダ
            var dirs =
                favorites
                .Where(x => x.Path.Length > Path.Length && x.Path.Take(Path.Length).SequenceEqual(Path))
                .GroupBy(x => x.Path[Path.Length]);
            foreach (var dir in dirs)
            {
                items.Add(new BrowserFavoriteDirectoryViewModel(string.Join('/', Path.Append(dir.Key)), [.. dir]));
            }
            //アイテム
            items.AddRange(favorites.Where(x => x.Path.Length == Path.Length && x.Path.Take(Path.Length).SequenceEqual(Path)));
            Items = [.. items];
        }
        public static BrowserFavoriteDirectoryViewModel CreateBrowserFavoriteRoot()
        {
            return new BrowserFavoriteDirectoryViewModel(string.Empty, [.. BrowserSettings.Default.Favorites.Select(x => new BrowserFavoriteItemViewModel(x))]);
        }
    }
}
