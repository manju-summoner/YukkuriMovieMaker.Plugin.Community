namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.CodexCLI
{
    internal class CodexCLISettings : SettingsBase<CodexCLISettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => Texts.CodexCLI;

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public string ApiKey { get; set => Set(ref field, value); } = string.Empty;

        public string Model { get; set => Set(ref field, value); } = string.Empty;

        public int TimeoutSeconds { get; set => Set(ref field, value); } = 120;

        public bool IsSendImageEnabled { get; set => Set(ref field, value); } = true;

        public ReasoningEffort ReasoningEffort { get; set => Set(ref field, value); } = ReasoningEffort.Low;

        public override void Initialize()
        {
            if (TimeoutSeconds <= 0)
                TimeoutSeconds = 120;
        }
    }
}
