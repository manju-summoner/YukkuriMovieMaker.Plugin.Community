using System;
using System.IO;

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

        public override void Initialize()
        {
        }
    }
}
