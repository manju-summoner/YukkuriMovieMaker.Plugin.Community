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

        public static string[] Models =
        [
            "grok-3",
            "grok-3-fast",
            "grok-3-mini",
            "grok-3-mini-fast",
            "grok-2-vision",
        ];
    }
}
