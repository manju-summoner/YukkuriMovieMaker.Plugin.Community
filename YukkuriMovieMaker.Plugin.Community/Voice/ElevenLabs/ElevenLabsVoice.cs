using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoice : Bindable
    {
        string? name, id;
        public string? Name { get => name; set => Set(ref name, value); }
        public string? Id { get => id; set => Set(ref id, value, nameof(Id), nameof(ExpirationWarning)); }

        /// <summary>
        /// 2026-12-31 で使えなくなるデフォルトのボイスなら、設定画面の話者の表に出す注意書き
        /// </summary>
        [JsonIgnore]
        public string? ExpirationWarning => ElevenLabsDefaultVoices.GetWarning(Id);

        public ElevenLabsVoice() { }
        public ElevenLabsVoice(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }
}
