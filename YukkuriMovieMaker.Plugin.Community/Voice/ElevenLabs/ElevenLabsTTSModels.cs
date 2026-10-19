using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsTTSModels
    {
        public static string[] Models { get; } = ["eleven_v4", "eleven_v3", "eleven_multilingual_v2", "eleven_flash_v2_5"];

        public static string Resolve(string? model) => string.IsNullOrWhiteSpace(model) ? Models.First() : model;

        //Eleven v4（eleven_v4_turboを含む）のvoice_settingsはstabilityとsimilarity_boostだけで、speed・style・use_speaker_boostに対応していない
        //https://elevenlabs.io/docs/overview/capabilities/text-to-speech/eleven-v4
        public static bool IsV4(string? model) => Resolve(model).StartsWith("eleven_v4", StringComparison.OrdinalIgnoreCase);
    }
}
