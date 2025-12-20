using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserSettings : SettingsBase<BrowserSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public ObservableCollection<BrowserFavorite> Favorites { get; } = [];

        public override void Initialize()
        {

        }
    }
}
