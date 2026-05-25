using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Browser
{
    class BrowserSettings : SettingsBase<BrowserSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Tool;

        public override string Name => Texts.Browser;

        public override bool HasSettingView => true;

        public override object? SettingView => new BrowserSettingsView() { DataContext = new BrowserSettingsViewModel() };

        public ObservableCollection<BrowserFavorite> Favorites { get; } = [];

        public bool IsSmartScreenEnabled { get; set => Set(ref field, value); } = true;
        
        public int TrackingPreventionLevel { get; set => Set(ref field, value); } = 1;

        public bool IsPasswordAutosaveEnabled { get; set => Set(ref field, value); } = false;

        public bool IsGeneralAutofillEnabled { get; set => Set(ref field, value); } = true;

        public bool IsScriptEnabled { get; set => Set(ref field, value); } = true;

        public bool IsMediaAutoplayEnabled 
        {
            get;
            set 
            {
                if (Set(ref field, value))
                    OnPropertyChanged(nameof(AdditionalBrowserArguments));
            } 
        } = true;

        public string CustomUserAgent { get; set => Set(ref field, value); } = string.Empty;

        public string ImageDownloadFolder { get => field; set => Set(ref field, value); } = string.Empty;

        [JsonIgnore]
        public string AdditionalBrowserArguments => IsMediaAutoplayEnabled ? string.Empty : "--autoplay-policy=document-user-activation-required";

        public override void Initialize()
        {

        }
    }
}
