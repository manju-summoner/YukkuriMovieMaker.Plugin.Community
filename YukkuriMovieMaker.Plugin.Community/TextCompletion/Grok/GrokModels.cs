using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokModels
    {
        public static string DefaultModel = "grok-3-mini-fast";

        public static GrokModel[] Models =
        [
            new GrokModel("grok-4", true),
            new GrokModel("grok-3", false),
            new GrokModel("grok-3-fast", false),
            new GrokModel("grok-3-mini", true),
            new GrokModel("grok-3-mini-fast", true),
            new GrokModel("grok-2-vision", false),
        ];

        internal static GrokModel? FindModel(string? model)
        {
            return Models.FirstOrDefault(x => x.Key.Equals(model, StringComparison.OrdinalIgnoreCase));
        }
    }
}
