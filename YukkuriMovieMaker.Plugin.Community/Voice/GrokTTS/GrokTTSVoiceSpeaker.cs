using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    //https://docs.x.ai/developers/model-capabilities/audio/text-to-speech
    internal class GrokTTSVoiceSpeaker(string voice) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(1);

        public string EngineName => API;

        public string SpeakerName => voice;

        public string API => "GrokTTS";

        public string ID => voice;

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            return Task.FromResult(text);
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if (parameter is not GrokTTSVoiceParameter grokParameter)
                grokParameter = new GrokTTSVoiceParameter();

            var apiKey = GrokTTSSettings.Default.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Grok TTS APIキーが設定されていません。");

            await semaphore.WaitAsync();
            try
            {
                var url = "https://api.x.ai/v1/tts";

                var requestJson = new JObject
                {
                    ["text"] = text,
                    ["voice_id"] = voice,
                    ["language"] = string.IsNullOrWhiteSpace(grokParameter.Language) ? "auto" : grokParameter.Language,
                    ["output_format"] = new JObject
                    {
                        ["codec"] = "wav",
                        ["sample_rate"] = 44100,
                    },
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = new StringContent(requestJson.ToString(), Encoding.UTF8, "application/json");

                var client = HttpClientFactory.Client;
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.StatusCode is not HttpStatusCode.OK)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    string? message = null;
                    try
                    {
                        var error = JObject.Parse(errorText);
                        message =
                            error["error"]?["message"]?.Value<string>()
                            ?? error["error"]?.Value<string>()
                            ?? error["message"]?.Value<string>();
                    }
                    catch
                    {
                        // not JSON
                    }
                    if (!string.IsNullOrEmpty(message))
                        throw new Exception(message);
                    response.EnsureSuccessStatusCode();
                }

                using var srcStream = await response.Content.ReadAsStreamAsync();
                using var destStream = File.Create(filePath);
                await srcStream.CopyToAsync(destStream);
            }
            finally
            {
                semaphore.Release();
            }
            return null;
        }

        public IVoiceParameter CreateVoiceParameter() => new GrokTTSVoiceParameter();

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not GrokTTSVoiceParameter)
                return new GrokTTSVoiceParameter();
            return currentParameter;
        }
    }
}
