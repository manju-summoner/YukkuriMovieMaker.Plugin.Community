using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    class ExplorerSettings : SettingsBase<ExplorerSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public bool IsAlwaysShowToolBar { get; set => Set(ref field, value); } = true;

        public ObservableCollection<ExplorerFavorite> Favorites { get; } = [];

        public override void Initialize()
        {

        }
    }
}
