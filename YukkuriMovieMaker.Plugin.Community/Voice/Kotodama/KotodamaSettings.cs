using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class KotodamaSettings : SettingsBase<KotodamaSettings>
    {

        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "Kotodama";

        public override bool HasSettingView => true;

        public override object? SettingView => new KotodamaSettingsView();

        public string? ApiKey { get; set => Set(ref field, value); }

        public ImmutableList<KotodamaSpeakerSettings> Speakers { get; set => Set(ref field, value); } = [];

        public override void Initialize()
        {
            if(Speakers.IsEmpty)
                Speakers = [.. KotodamaDefaultSpeaker.AllSpeakers.Select(x=>new KotodamaSpeakerSettings(x))];
        }
    }
}
