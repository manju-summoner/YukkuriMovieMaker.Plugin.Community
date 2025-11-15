using Newtonsoft.Json;
using System.Runtime.Serialization;
using Newtonsoft.Json.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum SpeechSynthesisDestination
    {
        [EnumMember(Value = "audio_device")]
        AudioDevice,
        [EnumMember(Value = "file")]
        File
    }
}
