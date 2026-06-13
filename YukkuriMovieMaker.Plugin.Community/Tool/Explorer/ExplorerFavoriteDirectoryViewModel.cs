namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    class ExplorerFavoriteDirectoryViewModel : IExplorerFavoriteItemViewModel
    {
        public string Display { get; }
        public string[] Path { get; }
        public IExplorerFavoriteItemViewModel[] Items { get; }

        public ExplorerFavoriteDirectoryViewModel(string path, ExplorerFavoriteItemViewModel[] favorites)
        {
            Path = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            Display = Path.Length is 0 ? string.Empty : Path[^1];

            var items = new List<IExplorerFavoriteItemViewModel>();
            //フォルダ
            var dirs =
                favorites
                .Where(x => x.Path.Length > Path.Length && x.Path.Take(Path.Length).SequenceEqual(Path))
                .GroupBy(x => x.Path[Path.Length]);
            foreach (var dir in dirs)
            {
                items.Add(new ExplorerFavoriteDirectoryViewModel(string.Join('/', Path.Append(dir.Key)), [.. dir]));
            }
            //アイテム
            items.AddRange(favorites.Where(x => x.Path.Length == Path.Length && x.Path.Take(Path.Length).SequenceEqual(Path)));
            Items = [.. items];
        }
        public static ExplorerFavoriteDirectoryViewModel CreateExplorerFavoriteRoot()
        {
            return new ExplorerFavoriteDirectoryViewModel(string.Empty, [.. ExplorerSettings.Default.Favorites.Select(x => new ExplorerFavoriteItemViewModel(x))]);
        }
    }
}
