using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum RequestState
    {
        [EnumMember(Value = "queued")]
        Queued,
        [EnumMember(Value = "running")]
        Running,
        [EnumMember(Value = "succeeded")]
        Succeeded,
        [EnumMember(Value = "failed")]
        Failed
    }
}
