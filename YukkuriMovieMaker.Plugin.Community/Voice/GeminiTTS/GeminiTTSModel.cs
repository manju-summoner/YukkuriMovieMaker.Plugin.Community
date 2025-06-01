using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSModel(string name, RateLimits rpm)
    {
        public static string DefaultModelName = "gemini-2.5-flash-preview-tts";
        public static GeminiTTSModel[] Models = [
            new GeminiTTSModel("gemini-2.5-pro-preview-tts", new RateLimits(0, 10,100,100)),
            new GeminiTTSModel("gemini-2.5-flash-preview-tts", new RateLimits(3, 10,1000,1000))
        ];
        public static GeminiTTSModel? GetModel(string name) => Models.FirstOrDefault(m => m.Name == name);
        public static GeminiTTSModel DefaultModel => GetModel(DefaultModelName) ?? Models[0];

        public string Name { get; } = name;
        public RateLimits RPM { get; } = rpm;
    }
}
