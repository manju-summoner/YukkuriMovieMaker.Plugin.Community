using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiTextCompletionSettings : SettingsBase<GeminiTextCompletionSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public string APIKey { get; set; } = string.Empty;
        public string Model { get; set; } = GeminiModels.DefaultModel;
        
        bool isPreviewModel = false;
        public bool IsPreviewModel { get => isPreviewModel; set => Set(ref isPreviewModel, value); }

        public bool IsSendImageEnabled { get; set; } = false;

        double temperature = 1, topP = 0.95;
        int topK = 40;

        public double Temperature { get => temperature; set => Set(ref temperature, value); }
        public double TopP { get => topP; set => Set(ref topP, value); }
        public int TopK { get => topK; set => Set(ref topK, value); }

        SafetyLevel
            harassment = SafetyLevel.BlockSome,
            hateSpeech = SafetyLevel.BlockSome,
            sexuallyExplicit = SafetyLevel.BlockSome,
            dangerousContent = SafetyLevel.BlockSome,
            civicIntegrity = SafetyLevel.BlockSome;

        public SafetyLevel Harassment { get => harassment; set => Set(ref harassment, value); }
        public SafetyLevel HateSpeech { get => hateSpeech; set => Set(ref hateSpeech, value); }
        public SafetyLevel SexuallyExplicit { get => sexuallyExplicit; set => Set(ref sexuallyExplicit, value); }
        public SafetyLevel DangerousContent { get => dangerousContent; set => Set(ref dangerousContent, value); }
        public SafetyLevel CivicIntegrity { get => civicIntegrity; set => Set(ref civicIntegrity, value); }


        public override void Initialize()
        {

        }
    }
}
