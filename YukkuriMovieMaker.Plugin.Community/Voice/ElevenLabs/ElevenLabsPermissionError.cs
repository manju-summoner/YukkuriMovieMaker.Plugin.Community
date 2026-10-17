using Newtonsoft.Json.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    /// <summary>
    /// 権限を絞った API キーで呼ぶと、401 で detail.status が missing_permissions、detail.message に足りない権限の名前
    /// （一覧は voices_read、読み上げは text_to_speech）が返る。
    /// API の英語のメッセージでは ElevenLabs のどの設定を直せばよいか分からないので、API キーの設定画面の項目名で案内する
    /// </summary>
    internal static class ElevenLabsPermissionError
    {
        public static string? GetMessage(string responseText)
        {
            JObject? detail;
            try
            {
                detail = JObject.Parse(responseText)["detail"] as JObject;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }

            if (GetString(detail, "status") != "missing_permissions")
                return null;
            var message = GetString(detail, "message");
            if (message.Contains("voices_read", StringComparison.Ordinal))
                return Texts.MissingVoicesReadPermission;
            if (message.Contains("text_to_speech", StringComparison.Ordinal))
                return Texts.MissingTextToSpeechPermission;
            return null;
        }

        static string GetString(JObject? obj, string name) =>
            obj?[name] is JValue { Type: JTokenType.String } value ? (string?)value ?? string.Empty : string.Empty;
    }
}
