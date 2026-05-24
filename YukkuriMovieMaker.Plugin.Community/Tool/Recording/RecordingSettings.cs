using System;
using System.IO;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    internal class RecordingSettings : SettingsBase<RecordingSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => "Recording";
        public override bool HasSettingView => false;
        public override object? SettingView => null;

        public static string GetDefaultOutputDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "YMM4",
                "Recording");
        }

        public string OutputDirectory
        {
            get => outputDirectory;
            set => Set(ref outputDirectory, value);
        }

        private string outputDirectory = GetDefaultOutputDirectory();

        public string DefaultVoiceAudioFilePath
        {
            get => defaultVoiceAudioFilePath;
            set => Set(ref defaultVoiceAudioFilePath, value ?? string.Empty);
        }

        private string defaultVoiceAudioFilePath = string.Empty;

        public string SelectedRecordingDeviceId
        {
            get => selectedRecordingDeviceId;
            set => Set(ref selectedRecordingDeviceId, value ?? RecordingService.DefaultRecordingDeviceId);
        }

        private string selectedRecordingDeviceId = RecordingService.DefaultRecordingDeviceId;

        public override void Initialize()
        {
        }
    }
}
