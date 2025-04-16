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
                voice_settings = parameter.CreateVoiceSettings(),
            };
            var jsonString = Json.Json.GetJsonText(json, new Newtonsoft.Json.JsonSerializerSettings());
            request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            if(!response.IsSuccessStatusCode)
            {
                var resJsonText = await response.Content.ReadAsStringAsync();
                var resJson = JObject.Parse(resJsonText);
                var status = resJson?["detail"]?["status"]?.Value<string>();
                var message = resJson?["detail"]?["message"]?.Value<string>();
                throw new ElevenLabsException(status, message);
            }
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await responseStream.CopyToAsync(fileStream);
        }
    }

    public class ElevenLabsException(string? status, string? message) : Exception($"[{status ?? "null"}] {message ?? "null"}");
}
