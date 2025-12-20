using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    class ExplorerFavorite : Bindable
    {
        public string Directory { get; set => Set(ref field, value); } = string.Empty;
        public string Name { get; set => Set(ref field, value); } = string.Empty;
        public string Url { get; set => Set(ref field, value); } = string.Empty;
    }
}