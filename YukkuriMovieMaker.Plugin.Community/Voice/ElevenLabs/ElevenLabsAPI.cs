using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsAPI(string apiKey)
    {
        readonly string apiKey = apiKey;
        public ElevenLabsAPI() : this(ElevenLabsSettings.Default.APIKey) { }

        public async Task TextToSpeechAsync(string model, string voiceId, string speechText, ElevenLabsVoiceParameter parameter, string outputPath)
        {
            //https://elevenlabs.io/docs/api-reference/text-to-speech/convert

            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}?output_format=mp3_44100_128";

            var client = HttpClientFactory.Client;
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("xi-api-key", apiKey);

            var json = new
            {
                text = speechText,
                model_id = model,
                voice_settings = parameter.CreateVoiceSettings(model),
            };
            var jsonString = Json.Json.GetJsonText(json, new Newtonsoft.Json.JsonSerializerSettings());
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            if(!response.IsSuccessStatusCode)
            {
                var resJsonText = await response.Content.ReadAsStringAsync();
                throw CreateTextToSpeechError(resJsonText, voiceId);
            }
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await responseStream.CopyToAsync(fileStream);
        }

        /// <summary>
        /// 読み上げに失敗したときの例外。API キーの権限不足なら直し方の案内だけを出す（どの声でも失敗するので、声についての案内より先に直す必要がある）。
        /// それ以外は API のエラーに、期限切れになるデフォルトのボイスならその理由を添える
        /// </summary>
        internal static ElevenLabsException CreateTextToSpeechError(string responseText, string voiceId)
        {
            var resJson = JObject.Parse(responseText);
            var status = resJson?["detail"]?["status"]?.Value<string>();
            var message = resJson?["detail"]?["message"]?.Value<string>();
            //API の英語のメッセージはログで原因を追えるように内側の例外に残す
            if (ElevenLabsPermissionError.GetMessage(responseText) is { } permissionMessage)
                return new ElevenLabsException(permissionMessage, new ElevenLabsException(status, message));
            return new ElevenLabsException(status, message, ElevenLabsDefaultVoices.Find(voiceId) is { } expiring ? string.Format(Texts.DefaultVoiceExpirationError, expiring.Name, expiring.ReplacementName) : null);
        }
    }

    public class ElevenLabsException : Exception
    {
        public ElevenLabsException(string? status, string? message) : base(FormatMessage(status, message, null)) { }

        /// <param name="hint">API のエラーに添える、YMM4 側で分かる原因（期限切れになったデフォルトのボイスなど）</param>
        public ElevenLabsException(string? status, string? message, string? hint) : base(FormatMessage(status, message, hint)) { }

        /// <param name="message">API のエラーの代わりにそのまま表示する文面（API キーの権限不足の案内など）</param>
        /// <param name="innerException">元の API のエラー</param>
        /// <remarks>public にすると、外部のコードの new ElevenLabsException(status, null) が (string?, string?) とあいまいになってビルドできなくなるので internal にしている</remarks>
        internal ElevenLabsException(string message, Exception innerException) : base(message, innerException) { }

        static string FormatMessage(string? status, string? message, string? hint)
        {
            var text = $"[{status ?? "null"}] {message ?? "null"}";
            return string.IsNullOrEmpty(hint) ? text : text + Environment.NewLine + hint;
        }
    }
}
