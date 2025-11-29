using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.Code;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokTextCompletionPlugin : LlmTornadoTextCompletionPluginBase
    {
        public override object? SettingsView => new GrokSettingsView();

        public override string Name => "Grok";

        protected override ChatRequestVendorExtensions? CreateVendorExtensions()
        {
            return new ChatRequestVendorExtensions(
                new ChatRequestVendorXAiExtensions()
                {
                    
                });
        }

        protected override LlmTornadoTextCompletionGeneralSettings GetGeneralSettings()
        {
            return GrokSettings.Default.RequestSettings.GeneralSettings;
        }

        protected override LLmProviders GetProvider() => LLmProviders.XAi;
    }
}
