using System;
using System.Collections.Generic;
using System.Text;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkSettings : SettingsBase<VoiSonaTalkSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "VoiSona Talk";

        public override bool HasSettingView => true;

        public override object? SettingView => new VoiSonaTalkSettingsView();
        
        string? userName, password, appPath = @"C:\Program Files\Techno-Speech\VoiSona Talk\VoiSona Talk.exe";
        int port = 32766;
        bool isVoicesCahced = false;
        VoiceInformation[] voices = [];

        public string? AppPath { get=> appPath; set => Set(ref appPath, value); }
        public string? UserName { get=> userName; set => Set(ref userName, value); }
        public string? Password { get=> password; set => Set(ref password, value); }
        public int Port { get => port; set => Set(ref port, value); }
        public VoiceInformation[] Voices { get => voices; set => Set(ref voices, value); }
        public bool IsVoicesCached { get => isVoicesCahced;set => Set(ref isVoicesCahced, value); }

        public override void Initialize()
        {

        }
    }
}
