using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiModels
    {
        public static string DefaultModel => "gemini-2.5-flash";

        public static GeminiModel[] Models =>
            [
                new GeminiModel("gemini-2.5-pro", true, 128),
                new GeminiModel("gemini-2.5-flash", true, 0),
                new GeminiModel("gemini-2.5-flash-lite", true, 0),

                new GeminiModel("gemini-2.0-flash", false, null),
                new GeminiModel("gemini-2.0-flash-lite", false, null),
            ];
        public static GeminiModel? FindModel(string key)
        {
            return Models.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
