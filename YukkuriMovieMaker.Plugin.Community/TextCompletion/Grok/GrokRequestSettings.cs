using LlmTornado.Chat.Models.XAi;
using LlmTornado.Code;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokRequestSettings : Bindable
    {
        public LlmTornadoTextCompletionGeneralSettings GeneralSettings { get; } = new(ChatModelXAiGrok4.ModelV4FastNonReasoning);

        #region 古いAPI
        [Obsolete]
        public string? Model
        {
            set
            {
                if(!string.IsNullOrEmpty(value))
                    GeneralSettings.ModelName = value;
            }
        }
        [Obsolete]
        public double FrequencyPenalty { set => GeneralSettings.FrequencyPenalty = value; }
        [Obsolete]
        public double PresencePenalty { set => GeneralSettings.PresencePenalty = value; }
        [Obsolete]
        public double Temperature { set => GeneralSettings.Temperature = value; }
        #endregion
    }
}
