using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiTextCompletionPlugin : ITextCompletionPlugin2
    {
        public object? SettingsView => new GeminiTextCompletionSettingsView();

        public string Name => "Gemini";

        public Task<string> ProcessAsync(string systemPrompt, string text) => ProcessAsync(systemPrompt, text, null);

        public async Task<string> ProcessAsync(string systemPrompt, string text, Bitmap? image)
        {
            var json = CreateJson(systemPrompt, text, ref image);
            var jsonText = json.ToString();
            var jsonContent = new StringContent(jsonText, Encoding.UTF8, "application/json");

            var model = GeminiTextCompletionSettings.Default.Model;
            var endpoint = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", GeminiTextCompletionSettings.Default.APIKey);
            request.Content = jsonContent;

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            return
                responseJson["candidates"]
                ?.FirstOrDefault()
                ?["content"]
                ?["parts"]
                ?.FirstOrDefault()
                ?["text"]
                ?.Value<string>()
                ?? string.Empty;
        }

        private static JObject CreateJson(string systemPrompt, string text, ref Bitmap? image)
        {
            if (image != null)
            {
                var maxSize = 512;
                var scale = Math.Min(maxSize / (double)image.Width, maxSize / (double)image.Height);
                if (scale < 1)
                {
                    image = new Bitmap(image, new Size((int)(image.Width * scale), (int)(image.Height * scale)));
                }
            }

            var messagePart = new JObject()
            {
                ["text"] = systemPrompt + "\r\n" + text,
            };
            var imagePart = image != null ? new JObject()
            {
                ["inline_data"] = new JObject()
                {
                    ["mime_type"] = "image/jpeg",
                    ["data"] = BitmapToBase64(image),
                }
            } : null;
            JArray parts = imagePart is null ? [messagePart] : [messagePart, imagePart];

            var json = new JObject()
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = parts
                    }
                }
            };
            return json;
        }

        static string BitmapToBase64(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            stream.Flush();
            stream.Position = 0;
            return Convert.ToBase64String(stream.ToArray());
        }
    }
}
