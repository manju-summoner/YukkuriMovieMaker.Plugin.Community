using LlmTornado.Chat.Models;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Others
{
    internal class OthersSettings : SettingsBase<OthersSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => Texts.Others;

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public LlmTornadoTextCompletionGeneralSettings GeneralSettings { get; } = new(ChatModelAnthropicClaude45.ModelOpus251101);
        public LLmProviders Provider { get; set => Set(ref field, value); } = LLmProviders.Anthropic;
        public string? ServerUri { get; set => Set(ref field, value); } = "http://localhost:8000";

        public override void Initialize()
        {

        }
    }
}
