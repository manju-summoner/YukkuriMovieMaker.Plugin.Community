using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokRequestSettings : Bindable
    {
        string? model = GrokModels.DefaultModel;
        double 
            frequencyPenalty = 0,
            presencePenalty = 0,
            temperature = 1,
            topP = 1;

        public string? Model { get => model; set => Set(ref model, value); }

        public double FrequencyPenalty { get => frequencyPenalty; set => Set(ref frequencyPenalty, value); }
        public double PresencePenalty { get => presencePenalty; set => Set(ref presencePenalty, value); }
        public double Temperature { get => temperature; set => Set(ref temperature, value); }
        public double TopP { get => topP; set => Set(ref topP, value); }
    }
}
