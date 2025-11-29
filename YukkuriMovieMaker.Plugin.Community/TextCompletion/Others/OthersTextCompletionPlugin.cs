using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.Code;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Others
{
    [PluginOrder(int.MaxValue)]
    internal class OthersTextCompletionPlugin : LlmTornadoTextCompletionPluginBase
    {
        public override object? SettingsView => new OthersSettingsView();

        public override string Name => Texts.Others;

        protected override ChatRequestVendorExtensions? CreateVendorExtensions() => null;

        protected override LlmTornadoTextCompletionGeneralSettings GetGeneralSettings()
        {
            return OthersSettings.Default.GeneralSettings;
        }

        protected override LLmProviders GetProvider() => OthersSettings.Default.Provider;

        string _apiKeyCache = string.Empty;
        LLmProviders _providerCache = LLmProviders.Unknown;
        TornadoApi? _api;
        protected override TornadoApi EnsureApi(string apiKey)
        {
            if(string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException(string.Format(Texts.APIKeyIsNotSetMessage, Name));

            var provider = GetProvider();
            if(provider is not LLmProviders.Unknown and not LLmProviders.Custom and not LLmProviders.Length)
                return base.EnsureApi(apiKey);

            if (_api != null && _apiKeyCache == apiKey && _providerCache == provider)
                return _api;

            _apiKeyCache = apiKey;
            _providerCache = provider;
            _api = new TornadoApi(new Uri(OthersSettings.Default.ServerUri ?? string.Empty), apiKey);
            return _api;
        }
    }
}
