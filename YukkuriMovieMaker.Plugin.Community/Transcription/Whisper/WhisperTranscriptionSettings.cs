namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    internal class WhisperTranscriptionSettings : SettingsBase<WhisperTranscriptionSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public WhisperModel Model { get => model; set => Set(ref model, value); }
        WhisperModel model = WhisperModels.GetDefaultModel();

        public WhisperLanguage Language { get => language; set => Set(ref language, value); }
        WhisperLanguage language = WhisperLanguage.GetSystemLanguageOrAuto();

        public override void Initialize()
        {
            var models = WhisperModels.GetDefaultAndUserModels();
            if (!models.Contains(model))
            {
                //モデルが存在しない場合、同名モデル or デフォルトモデルにする
                model = models.Where(x => x.Name == model.Name).DefaultIfEmpty(WhisperModels.GetDefaultModel()).First();
            }
        }
    }
}
