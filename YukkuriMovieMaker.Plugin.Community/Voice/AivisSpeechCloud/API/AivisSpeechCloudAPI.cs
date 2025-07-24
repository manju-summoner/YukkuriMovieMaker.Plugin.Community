using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    internal class AivisSpeechCloudAPI(string apiKey)
    {
        static readonly string url = "https://api.aivis-project.com/v1";
        readonly string apiKey = apiKey;

        public async Task SynthesizeAsync(AivisSpeechCloudAPISynthesisParameters param, string filePath)
        {
            var client = HttpClientFactory.Client;

            var json = Json.Json.GetJsonText(param, new Newtonsoft.Json.JsonSerializerSettings()
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/tts/synthesize");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage? response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorJson = JObject.Parse(errorContent);
                var message = errorJson["detail"]?.Value<string>();
                if (string.IsNullOrEmpty(message))
            response.EnsureSuccessStatusCode();
                throw new Exception(message);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = System.IO.File.Create(filePath);
            await stream.CopyToAsync(fileStream);
        }
        public static async Task<AivisSpeechCloudAPIModelInfo> GetModelInfoAsync(string modelUuid)
        {
            var client = HttpClientFactory.Client;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/aivm-models/{modelUuid}");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return Json.Json.LoadFromText<AivisSpeechCloudAPIModelInfo>(json) ?? throw new NullReferenceException();
        }
    }
}
