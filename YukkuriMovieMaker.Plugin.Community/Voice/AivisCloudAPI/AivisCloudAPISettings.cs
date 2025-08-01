using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI
{
    internal class AivisCloudAPISettings : SettingsBase<AivisCloudAPISettings>
    {
        public string ApiKey { get; set; } = string.Empty;

        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "Aivis Cloud API";

        public override bool HasSettingView => true;

        public override object? SettingView => new AivisCloudAPISettingsView();

        public ObservableCollection<API.ModelInfoContract> Models { get; } = [];

        public override void Initialize()
        {
            if(Models.Count is 0)
            {
                foreach(var model in API.ModelInfoContract.GetDefaultModels())
                    Models.Add(model);
            }
        }
    }
}
