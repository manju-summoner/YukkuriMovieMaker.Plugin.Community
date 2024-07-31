using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiModels
    {
        public static string[] Models => 
            [
                "gemini-1.5-flash",
                "gemini-1.5-pro",
            ];
    }
}
