using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    /// <summary>
    /// 設定の「話者一覧」に手で入力した声。Google AI Studio で作った声やライブラリの声の ID を入れる
    /// </summary>
    internal class GeminiTTSVoice : Bindable
    {
        string? name, id;
        public string? Name { get => name; set => Set(ref name, value); }
        public string? Id { get => id; set => Set(ref id, value); }

        public GeminiTTSVoice() { }
        public GeminiTTSVoice(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }
}
