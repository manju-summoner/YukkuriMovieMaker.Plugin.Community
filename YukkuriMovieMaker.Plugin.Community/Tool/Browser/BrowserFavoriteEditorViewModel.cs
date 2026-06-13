using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    internal class BrowserFavoriteEditorViewModel(BrowserFavorite favorite) : Bindable
    {
        public string Directory { get => favorite.Directory; set => Set(() => favorite.Directory, value); }
        public string Name { get => favorite.Name; set => Set(() => favorite.Name, value); }
        public string Url { get => favorite.Url; set => Set(() => favorite.Url, value); }
        public ICommand DeleteCommand { get; } = new ActionCommand(_ => true, _ => BrowserSettings.Default.Favorites.Remove(favorite));
    }
}
