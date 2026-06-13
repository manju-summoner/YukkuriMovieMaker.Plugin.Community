namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal record ExplorerState(string Location, ExplorerLayout Layout, ExplorerFilter Filter)
    {
        public ExplorerSortKey SortKey { get; init; } = ExplorerSortKey.Name;
        public ExplorerSortOrder SortOrder { get; init; } = ExplorerSortOrder.Ascending;
        public double SidebarWidth { get; init; } = 150;
        public bool IsSidebarVisible { get; init; } = false;
    }
}
