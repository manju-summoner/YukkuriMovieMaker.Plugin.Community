namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadSettings : SettingsBase<NotepadSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => throw new NotImplementedException();
        public override bool HasSettingView => false;
        public override object? SettingView => throw new NotImplementedException();

        public override void Initialize()
        {
        }
    }
}
