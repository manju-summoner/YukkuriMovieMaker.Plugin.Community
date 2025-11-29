using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiTextCompletionSettings : SettingsBase<GeminiTextCompletionSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public LlmTornadoTextCompletionGeneralSettings GeneralSettings { get; } = new LlmTornadoTextCompletionGeneralSettings(ChatModelGoogleGemini.ModelGemini25Flash);

        public SafetyLevel Harassment { get; set => Set(ref field, value); } = SafetyLevel.BlockSome;
        public SafetyLevel HateSpeech { get; set => Set(ref field, value); } = SafetyLevel.BlockSome;
        public SafetyLevel SexuallyExplicit { get; set => Set(ref field, value); } = SafetyLevel.BlockSome;
        public SafetyLevel DangerousContent { get; set => Set(ref field, value); } = SafetyLevel.BlockSome;


        public override void Initialize()
        {

        }

        #region 古いAPI
        [Obsolete]
        public string APIKey
        {
            set => GeneralSettings.ApiKey = value;
        }
        [Obsolete]
        public string Model 
        {
            set => GeneralSettings.ModelName = value;
        }
        [Obsolete]
        public bool IsSkipReasoningProcess
        {
            set => GeneralSettings.IsSkipReasoningProcess = value;
        }
        [Obsolete]
        public bool IsSendImageEnabled
        {
            set => GeneralSettings.IsSendImageEnabled = value;
        }
        [Obsolete]
        public double Temperature
        {
            set => GeneralSettings.Temperature = value;
        }
        #endregion
    }
}
