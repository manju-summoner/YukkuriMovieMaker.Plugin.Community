using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Http;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    //https://ai.google.dev/gemini-api/docs/speech-generation
    internal static class GeminiTTSVoiceList
    {
        //API の上限。全件で2000件を超えるので、上限で取っても数ページになる
        const int PageSize = 1000;

        /// <summary>
        /// プリセット30種は一覧 API では小文字の ID（kore）で返るが、YMM4 では既存のプロジェクトとの互換のため Kore のまま扱う
        /// </summary>
        public static bool IsPreset(string id) => GeminiTTSVoices.Voices.Contains(id, StringComparer.OrdinalIgnoreCase);

        public static (List<GeminiTTSVoiceInfo> Voices, string? NextPageToken) ParsePage(string json)
        {
            var root = JObject.Parse(json);
            var voices = new List<GeminiTTSVoiceInfo>();
            foreach (var item in root["voices"]?.OfType<JObject>() ?? [])
            {
                var id = item.Value<string>("id");
                if (string.IsNullOrEmpty(id))
                    continue;
                voices.Add(new GeminiTTSVoiceInfo
                {
                    Id = id,
                    DisplayName = item.Value<string>("display_name") ?? string.Empty,
                    LanguageCode = item.Value<string>("language_code") ?? string.Empty,
                    Accent = item.Value<string>("accent") ?? string.Empty,
                    Gender = item.Value<string>("gender") ?? string.Empty,
                    Type = item.Value<string>("type") ?? string.Empty,
                    Pitch = item.Value<string>("pitch") ?? string.Empty,
                    Persona = item.Value<string>("persona") ?? string.Empty,
                    Description = item.Value<string>("description") ?? string.Empty,
                });
            }
            var token = root.Value<string>("next_page_token");
            return (voices, string.IsNullOrEmpty(token) ? null : token);
        }

        /// <param name="getPage">ページトークン（最初のページは null）を受け取り、そのページの応答の JSON を返す</param>
        public static async Task<List<GeminiTTSVoiceInfo>> FetchAllAsync(Func<string?, Task<string>> getPage)
        {
            var result = new List<GeminiTTSVoiceInfo>();
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

        public static Task<List<GeminiTTSVoiceInfo>> FetchAllAsync(string apiKey) => FetchAllAsync(token => GetPageAsync(apiKey, token));

        static async Task<string> GetPageAsync(string apiKey, string? token)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/voices?page_size={PageSize}";
            if (token is not null)
                url += "&page_token=" + Uri.EscapeDataString(token);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            using var response = await HttpClientFactory.Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = TryGetErrorMessage(text);
                if (!string.IsNullOrEmpty(message))
                    throw new Exception(message);
                response.EnsureSuccessStatusCode();
            }
            return text;
        }

        public static string? TryGetErrorMessage(string responseText)
        {
            try
            {
                return JObject.Parse(responseText)["error"]?["message"]?.Value<string>();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// 話者一覧に出す声。自作ボイス（設定に追加した順）→ プリセット30種（今の順）→ ライブラリの声（言語 → 表示名の順）
        /// </summary>
        public static List<(string Id, string Name)> CreateSpeakerList(IEnumerable<GeminiTTSVoiceInfo> selectedVoices)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selected = selectedVoices
                .Where(v => !string.IsNullOrEmpty(v.Id) && !IsPreset(v.Id) && seen.Add(v.Id))
                .ToList();

            return
            [
                .. selected.Where(v => v.IsCustom).Select(v => (v.Id, v.SpeakerName)),
                .. GeminiTTSVoices.Voices.Select(v => (v, v)),
                .. OrderLibraryVoices(selected.Where(v => !v.IsCustom)).Select(v => (v.Id, v.SpeakerName)),
            ];
        }

        /// <summary>
        /// 声を選ぶ画面に並べる声。プリセット30種は常に話者一覧に出るので除き、自作ボイスを API の順のまま先頭に置く
        /// </summary>
        public static List<GeminiTTSVoiceInfo> CreateCatalog(IEnumerable<GeminiTTSVoiceInfo> voices)
        {
            var list = voices.Where(v => !IsPreset(v.Id)).ToList();
            return [.. list.Where(v => v.IsCustom), .. OrderLibraryVoices(list.Where(v => !v.IsCustom))];
        }

        static IEnumerable<GeminiTTSVoiceInfo> OrderLibraryVoices(IEnumerable<GeminiTTSVoiceInfo> voices) =>
            voices
            .OrderBy(v => v.LanguageCode, StringComparer.Ordinal)
            .ThenBy(v => v.DisplayName, NaturalStringComparer.Instance)
            .ThenBy(v => v.Id, StringComparer.Ordinal);

        /// <summary>
        /// 言語の絞り込みに使う主言語サブタグ（ja-JP → ja）
        /// </summary>
        public static string GetPrimaryLanguage(string languageCode)
        {
            var index = languageCode.IndexOf('-');
            return (index < 0 ? languageCode : languageCode[..index]).ToLowerInvariant();
        }

        /// <param name="language">主言語サブタグ。空ならすべての言語</param>
        /// <param name="gender">female / male / neutral。空ならすべて</param>
        /// <param name="search">表示名・ID・なまり・ペルソナ・説明の部分一致。空なら絞り込まない</param>
        public static bool IsMatch(GeminiTTSVoiceInfo voice, string language, string gender, string search)
        {
            //自作ボイスはユーザー自身が作った声なので、言語では隠さない
            if (!voice.IsCustom && !string.IsNullOrEmpty(language) && GetPrimaryLanguage(voice.LanguageCode) != language)
                return false;
            if (!string.IsNullOrEmpty(gender) && voice.Gender != gender)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;
            var keyword = search.Trim();
            return new[] { voice.DisplayName, voice.Id, voice.Accent, voice.Persona, voice.Description }
                .Any(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 声を選ぶ画面の結果を、設定の「話者一覧」に反映する。
        /// 一覧に無い声（Google 側で削除された声など）はユーザーが自分で削除するまで残す。
        /// 黙って消すと、その声を使うキャラクターが次回の起動時に別の声へ置き換わってしまうため。
        /// </summary>
        public static void ApplySelection(IList<GeminiTTSVoiceInfo> selectedVoices, IEnumerable<GeminiTTSVoiceInfo> catalog, IEnumerable<string> checkedIds)
        {
            var catalogById = new Dictionary<string, GeminiTTSVoiceInfo>();
            foreach (var voice in catalog)
                catalogById.TryAdd(voice.Id, voice);
            var checkedSet = checkedIds.ToHashSet();

            for (int i = selectedVoices.Count - 1; i >= 0; i--)
            {
                var id = selectedVoices[i].Id;
                if (!catalogById.TryGetValue(id, out var latest))
                    continue;
                if (checkedSet.Contains(id))
                    selectedVoices[i] = latest.CopyPersistentFields();
                else
                    selectedVoices.RemoveAt(i);
            }

            var existing = selectedVoices.Select(v => v.Id).ToHashSet();
            foreach (var id in checkedSet.Where(catalogById.ContainsKey).Where(existing.Add))
                selectedVoices.Add(catalogById[id].CopyPersistentFields());
        }

        /// <summary>
        /// 「Podcaster 2」が「Podcaster 10」より前に来るように、数字の部分を数の大きさで比べる
        /// </summary>
        internal sealed class NaturalStringComparer : IComparer<string>
        {
            public static NaturalStringComparer Instance { get; } = new();

            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                int i = 0, j = 0;
                while (i < x.Length && j < y.Length)
                {
                    if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
                    {
                        int si = i, sj = j;
                        while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                        while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                        var a = x[si..i].TrimStart('0');
                        var b = y[sj..j].TrimStart('0');
                        var result = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                        if (result != 0) return result;
                    }
                    else
                    {
                        var result = string.Compare(x[i].ToString(), y[j].ToString(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
                        if (result != 0) return result;
                        i++;
                        j++;
                    }
                }
                return (x.Length - i).CompareTo(y.Length - j);
            }
        }
    }
}
