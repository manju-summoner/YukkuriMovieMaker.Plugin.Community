using Newtonsoft.Json.Linq;
using System.Net.Http;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    //https://elevenlabs.io/docs/api-reference/voices/search
    internal static class ElevenLabsVoiceList
    {
        //API の上限
        const int PageSize = 100;

        public static (List<ElevenLabsVoiceInfo> Voices, string? NextPageToken) ParsePage(string json)
        {
            var root = JObject.Parse(json);
            var voices = new List<ElevenLabsVoiceInfo>();
            foreach (var item in root["voices"]?.OfType<JObject>() ?? [])
            {
                var id = GetString(item, "voice_id");
                if (string.IsNullOrEmpty(id))
                    continue;
                var labels = item["labels"] as JObject;
                var description = GetString(item, "description");
                voices.Add(new ElevenLabsVoiceInfo
                {
                    VoiceId = id,
                    Name = GetString(item, "name"),
                    Category = GetString(item, "category"),
                    IsOwner = item["is_owner"]?.Type == JTokenType.Boolean ? item.Value<bool>("is_owner") : null,
                    Gender = GetString(labels, "gender"),
                    Age = GetString(labels, "age"),
                    Accent = GetString(labels, "accent"),
                    Language = GetString(labels, "language"),
                    Description = string.IsNullOrEmpty(description) ? GetString(labels, "description") : description,
                });
            }

            var token = GetString(root, "next_page_token");
            var hasMore = root["has_more"]?.Type == JTokenType.Boolean ? root.Value<bool>("has_more") : true;
            return (voices, hasMore && !string.IsNullOrEmpty(token) ? token : null);
        }

        static string GetString(JObject? obj, string name) =>
            obj?[name] is JValue { Type: JTokenType.String } value ? (string?)value ?? string.Empty : string.Empty;

        /// <param name="getPage">ページトークン（最初のページは null）を受け取り、そのページの応答の JSON を返す</param>
        public static async Task<List<ElevenLabsVoiceInfo>> FetchAllAsync(Func<string?, Task<string>> getPage)
        {
            var result = new List<ElevenLabsVoiceInfo>();
            var usedTokens = new HashSet<string>();
            string? token = null;
            do
            {
                var (voices, next) = ParsePage(await getPage(token));
                result.AddRange(voices);
                //同じトークンが返り続けると終わらないので、API の異常として止める
                if (next is not null && !usedTokens.Add(next))
                    throw new InvalidOperationException($"The voice list API returned the same page token twice: {next}");
                token = next;
            }
            while (token is not null);
            return result;
        }

        public static Task<List<ElevenLabsVoiceInfo>> FetchAllAsync(string apiKey) => FetchAllAsync(token => GetPageAsync(apiKey, token));

        static async Task<string> GetPageAsync(string apiKey, string? token)
        {
            var url = $"https://api.elevenlabs.io/v2/voices?page_size={PageSize}&include_total_count=false";
            if (token is not null)
                url += "&next_page_token=" + Uri.EscapeDataString(token);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("xi-api-key", apiKey);
            using var response = await HttpClientFactory.Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = TryGetErrorMessage(text);
                //API の英語のメッセージはログで原因を追えるように内側の例外に残す
                if (ElevenLabsPermissionError.GetMessage(text) is { } permissionMessage)
                    throw new Exception(permissionMessage, message is null ? null : new Exception(message));
                if (!string.IsNullOrEmpty(message))
                    throw new Exception(message);
                response.EnsureSuccessStatusCode();
            }
            return text;
        }

        /// <summary>
        /// detail はオブジェクト（status と message）・文字列・配列（入力の検証エラー）のどれかで返る
        /// </summary>
        public static string? TryGetErrorMessage(string responseText)
        {
            JToken? detail;
            try
            {
                detail = JObject.Parse(responseText)["detail"];
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }

            return detail switch
            {
                JObject obj => FormatError(GetString(obj, "status"), GetString(obj, "message")),
                JValue { Type: JTokenType.String } value => (string?)value,
                JArray array => string.Join(Environment.NewLine, array.OfType<JObject>().Select(x => GetString(x, "msg")).Where(x => x.Length > 0)) is { Length: > 0 } joined ? joined : null,
                _ => null,
            };
        }

        static string? FormatError(string status, string message) =>
            (status.Length, message.Length) switch
            {
                (0, 0) => null,
                (0, _) => message,
                (_, 0) => status,
                _ => $"[{status}] {message}",
            };

        /// <summary>
        /// 声を選ぶ画面に並べる声。自作 → ライブラリ → デフォルトの順に、それぞれ名前の順で並べる。
        /// 期限切れになるデフォルトのボイスも、使えるうちは除かない。使えなくなれば一覧 API から返らなくなるため
        /// </summary>
        public static List<ElevenLabsVoiceInfo> CreateCatalog(IEnumerable<ElevenLabsVoiceInfo> voices)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return
            [
                .. voices
                .Where(v => seen.Add(v.VoiceId))
                .OrderBy(v => v.Kind)
                .ThenBy(v => v.SpeakerName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(v => v.VoiceId, StringComparer.Ordinal),
            ];
        }

        /// <param name="kind">null ならすべての種類</param>
        /// <param name="search">名前・ID・ラベル・説明の部分一致。空なら絞り込まない</param>
        public static bool IsMatch(ElevenLabsVoiceInfo voice, ElevenLabsVoiceKind? kind, string search)
        {
            if (kind is not null && voice.Kind != kind)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;
            var keyword = search.Trim();
            return new[] { voice.Name, voice.VoiceId, voice.Gender, voice.Age, voice.Accent, voice.Language, voice.Description }
                .Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 選んだ声を話者の表の末尾に足す。表に同じ voice_id がある声は足さない。
        /// 表の行は名前をユーザーが書き換えられるので、既存の行には触らない。
        /// </summary>
        /// <returns>足した数</returns>
        public static int AddVoices(IList<ElevenLabsVoice> speakers, IEnumerable<ElevenLabsVoiceInfo> selectedVoices)
        {
            var existing = GetRegisteredIds(speakers);
            var count = 0;
            foreach (var voice in selectedVoices)
            {
                if (!existing.Add(voice.VoiceId))
                    continue;
                speakers.Add(new ElevenLabsVoice(voice.SpeakerName, voice.VoiceId));
                count++;
            }
            return count;
        }

        /// <summary>
        /// 話者の表にある voice_id。読み上げでは表の ID がそのまま API に送られるので、前後に空白の付いた行は別の声として扱い、
        /// 正しい ID の声を選択画面から足し直せるようにする
        /// </summary>
        public static HashSet<string> GetRegisteredIds(IEnumerable<ElevenLabsVoice> speakers) =>
            speakers
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
