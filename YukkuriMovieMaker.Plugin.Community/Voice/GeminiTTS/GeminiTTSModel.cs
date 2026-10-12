using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSModel(string name, RateLimits rpm, bool usesSpeechMetadata = false)
    {
        public static string DefaultModelName = "gemini-3.8-flash-tts";
        public static GeminiTTSModel[] Models = [
            new GeminiTTSModel("gemini-2.5-pro-preview-tts", new RateLimits(0, 10,100,100)),
            new GeminiTTSModel("gemini-2.5-flash-preview-tts", new RateLimits(3, 10,1000,1000)),
            new GeminiTTSModel("gemini-3.1-flash-tts-preview", new RateLimits(3, 10,1000,1000)),
            new GeminiTTSModel("gemini-3.8-flash-tts", new RateLimits(3, 10,1000,1000), true),
            new GeminiTTSModel("gemini-3.8-flash-lite-tts", new RateLimits(3, 10,1000,1000), true)
        ];
        public static GeminiTTSModel? GetModel(string name) => Models.FirstOrDefault(m => m.Name == name);
        public static GeminiTTSModel DefaultModel => GetModel(DefaultModelName) ?? Models[0];

        public string Name { get; } = name;
        public RateLimits RPM { get; } = rpm;

        /// <summary>
        /// 3.8以降はテキストを一字一句そのまま読み上げるため、プロンプトは本文に混ぜず speech_metadata.style で渡す
        /// </summary>
        public bool UsesSpeechMetadata { get; } = usesSpeechMetadata;
    }
}
