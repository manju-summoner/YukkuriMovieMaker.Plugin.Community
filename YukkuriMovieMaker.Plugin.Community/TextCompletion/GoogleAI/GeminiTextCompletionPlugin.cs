using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiTextCompletionPlugin : LlmTornadoTextCompletionPluginBase
    {

        public override object? SettingsView => new GeminiTextCompletionSettingsView();

        public override string Name => "Gemini";

        protected override LlmTornadoTextCompletionGeneralSettings GetGeneralSettings() => GeminiTextCompletionSettings.Default.GeneralSettings;

        protected override ChatRequestVendorExtensions? CreateVendorExtensions()
        {
            return new ChatRequestVendorExtensions(
                new ChatRequestVendorGoogleExtensions() 
                {
                    SafetyFilters = new ChatRequestVendorGoogleSafetyFilters()
                    {
                        Harassment = ConvertToGoogleSafetyFilterType(GeminiTextCompletionSettings.Default.Harassment),
                        HateSpeech = ConvertToGoogleSafetyFilterType(GeminiTextCompletionSettings.Default.HateSpeech),
                        SexuallyExplicit = ConvertToGoogleSafetyFilterType(GeminiTextCompletionSettings.Default.SexuallyExplicit),
                        DangerousContent = ConvertToGoogleSafetyFilterType(GeminiTextCompletionSettings.Default.DangerousContent),
                    },
                    IncludeThoughts = false,
                });
        }

        static GoogleSafetyFilterTypes ConvertToGoogleSafetyFilterType(SafetyLevel level)
        {
            return level switch
            {
                SafetyLevel.BlockNone => GoogleSafetyFilterTypes.BlockNone,
                SafetyLevel.BlockFew => GoogleSafetyFilterTypes.BlockFew,
                SafetyLevel.BlockSome => GoogleSafetyFilterTypes.BlockSome,
                SafetyLevel.BlockMost => GoogleSafetyFilterTypes.BlockMost,
                _ => GoogleSafetyFilterTypes.BlockSome,
            };
        }

        protected override LLmProviders GetProvider() => LLmProviders.Google;
    }
}
