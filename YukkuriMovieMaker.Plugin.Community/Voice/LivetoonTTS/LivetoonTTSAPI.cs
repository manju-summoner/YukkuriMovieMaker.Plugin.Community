
using System.Net.Http;
using System.Net.Http.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.LivetoonTTS
{
    internal class LivetoonTTSAPI(string apiKey)
    {
        public async Task SynthesizeAsync(string text, string voice, double speed, string filePath)
        {
            var client = HttpClientFactory.Client;
            var url = "https://api.livetoon.net/v1/tts/livetoon";

            var requestBody = new
            {
                text,
                voice,
                speed,
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-API-Key", apiKey);
            request.Content = JsonContent.Create(requestBody);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                string errorMessage;
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorJson = System.Text.Json.JsonDocument.Parse(errorContent);
                    errorMessage = errorJson.RootElement.GetProperty("message").GetString() ?? "unknown";
                }
                catch
                {
                    errorMessage = await response.Content.ReadAsStringAsync();
                }
                throw new Exception($"status code: {response.StatusCode}\r\n{errorMessage}");
            }

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = System.IO.File.Create(filePath);
            await responseStream.CopyToAsync(fileStream);
        }
    }
}