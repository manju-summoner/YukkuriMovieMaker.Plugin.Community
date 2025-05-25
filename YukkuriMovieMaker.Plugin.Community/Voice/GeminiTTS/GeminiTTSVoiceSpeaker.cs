using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    //https://ai.google.dev/gemini-api/docs/speech-generation
    internal class GeminiTTSVoiceSpeaker(string voice) : IVoiceSpeaker
    {
        static readonly SemaphoreSlim semaphore = new(1);
        static DateTime lastRequest = DateTime.MinValue;
        public string EngineName => API;

        public string SpeakerName => voice;

        public string API => "GeminiTTS";

        public string ID => voice;

        public bool IsVoiceDataCachingRequired => true;

        public SupportedTextFormat Format => SupportedTextFormat.Text;

        public IVoiceLicense? License => null;

        public IVoiceResource? Resource => null;

        public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        {
            throw new NotImplementedException();
        }

        public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
        {
            if(parameter is not GeminiTTSVoiceParameter geminiTTSParameter)
                geminiTTSParameter = new GeminiTTSVoiceParameter();

            await semaphore.WaitAsync();
            try
            {
                await WaitRateLimitAsync();
                var prompt = geminiTTSParameter.Prompt;

                var model = GeminiTTSSettings.Default.Model;
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";


                var content = new StringContent(CreateJson(model, text, prompt), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", GeminiTTSSettings.Default.ApiKey);
                request.Content = content;

                var client = HttpClientFactory.Client;
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.StatusCode is not System.Net.HttpStatusCode.OK)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    var error = JObject.Parse(errorText);
                    var message = error["error"]?["message"]?.Value<string>();
                    if (!string.IsNullOrEmpty(message))
                        throw new Exception(message);
                }
                response.EnsureSuccessStatusCode();

                var responseJsonText = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseJsonText);
                var audioBase64 =
                    responseJson
                    ["candidates"]
                    ?.FirstOrDefault()
                    ?["content"]
                    ?["parts"]
                    ?.FirstOrDefault()
                    ?["inlineData"]
                    ?["data"]
                    ?.Value<string>()
                    ?? string.Empty;
                if (string.IsNullOrEmpty(audioBase64))
                    throw new Exception("音声データを取得できませんでした。");
                var mimeType =
                    responseJson
                    ["candidates"]
                    ?.FirstOrDefault()
                    ?["content"]
                    ?["parts"]
                    ?.FirstOrDefault()
                    ?["inlineData"]
                    ?["mimeType"]
                    ?.Value<string>()
                    ?? string.Empty;

                var audioBytes = Convert.FromBase64String(audioBase64);
                var supportedAudioFileMimeTypes = new[] { "audio/wav", "audio/ogg", "audio/mpeg" };

                if (mimeType.StartsWith("audio/L16"))
                    SavePCMData(filePath, mimeType, audioBytes);
                else if (supportedAudioFileMimeTypes.Any(mimeType.StartsWith))
                    File.WriteAllBytes(filePath, audioBytes);
                else
                    throw new NotSupportedException($"Unsupported audio format: {mimeType}");
            }
            finally
            {
                semaphore.Release();
            }
            return null;
        }

        static async Task WaitRateLimitAsync()
        {
            var model = GeminiTTSModel.GetModel(GeminiTTSSettings.Default.Model) ?? GeminiTTSModel.DefaultModel;
            var tier = GeminiTTSSettings.Default.Tier;
            var rpm = model.RPM.GetRateLimit(tier);
            var intervalPerRequest = TimeSpan.FromMinutes(1d / rpm);
            var elapsed = DateTime.Now - lastRequest;

            var requiredWait = intervalPerRequest - elapsed;
            if(requiredWait > TimeSpan.Zero)
                await Task.Delay(requiredWait);

            lastRequest = DateTime.Now;
        }

        static void SavePCMData(string filePath, string mimeType, byte[] audioBytes)
        {
            var sampleRate = 
                mimeType.Split(';')
                .Select(part => part.Split('='))
                .Where(kv => kv.Length is 2)
                .Select(kv => (key: kv[0].Trim(), value: kv[1].Trim()))
                .Where(x => x.key is "rate")
                .Select(x=>int.Parse(x.value))
                .First();

            const int fmtChunkSize = 16;
            const short format = 1; // PCM
            const short bitsPerSample = 16;
            const short channels = 1;

            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = channels * bitsPerSample / 8;
            int dataChunkSize = audioBytes.Length;
            int riffChunkSize = 36 + dataChunkSize;

            using var stream = File.Create(filePath);
            using var writer = new BinaryWriter(stream);

            // "RIFF" チャンク
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(riffChunkSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // "fmt " チャンク
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(fmtChunkSize);
            writer.Write(format);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // "data" チャンク
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataChunkSize);
            writer.Write(audioBytes);
        }

        string CreateJson(string model, string text, string prompt)
        {
            var json = new
            {
                model,
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = $"""
                                {prompt}
                                Speaker1: {text}
                                """,
                            },
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new
                        {
                            prebuiltVoiceConfig = new
                            {
                                voiceName = voice,
                            },
                        },
                    },
                }
            };

            return Json.Json.GetJsonText(json, new() { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None });
        }

        public IVoiceParameter CreateVoiceParameter() => new GeminiTTSVoiceParameter();

        public bool IsMatch(string api, string id)
        {
            return api == API && id == ID;
        }

        public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        {
            if (currentParameter is not GeminiTTSVoiceParameter)
                return new GeminiTTSVoiceParameter();
            return currentParameter;
        }
    }
}
