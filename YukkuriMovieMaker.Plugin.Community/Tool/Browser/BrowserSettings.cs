using Newtonsoft.Json;
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

        public bool IsSmartScreenEnabled { get => field; set => Set(ref field, value); } = true;
        
        public int TrackingPreventionLevel { get => field; set => Set(ref field, value); } = 1;

        public bool IsPasswordAutosaveEnabled { get => field; set => Set(ref field, value); } = false;

        public bool IsGeneralAutofillEnabled { get => field; set => Set(ref field, value); } = true;

        public bool IsScriptEnabled { get => field; set => Set(ref field, value); } = true;

        public bool IsMediaAutoplayEnabled 
        { 
            get => field; 
            set 
            {
                if (Set(ref field, value))
                    OnPropertyChanged(nameof(AdditionalBrowserArguments));
            } 
        } = true;

        public string CustomUserAgent { get => field; set => Set(ref field, value); } = string.Empty;

        [JsonIgnore]
        public string AdditionalBrowserArguments => IsMediaAutoplayEnabled ? string.Empty : "--autoplay-policy=document-user-activation-required";

        public override void Initialize()
        {

        }
    }
}
