using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.TextCompletion;
using static System.Net.Mime.MediaTypeNames;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokTextCompletionPlugin : ITextCompletionPlugin2
    {
        public object? SettingsView => new GrokSettingsView();

        public string Name => "Grok";

        public async Task<string> ProcessAsync(string systemPrompt, string text, Bitmap? image)
        {
            using var request = CreateRequest(systemPrompt, text, image);

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode is not System.Net.HttpStatusCode.OK)
            {
                var res = await response.Content.ReadAsStringAsync();
                var errorJson = JObject.Parse(res);
                var message = errorJson["error"]?.Value<string>();
                if (message is null)
                {
                    if (response.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
                        throw new InvalidOperationException("Rate limit exceeded");
                    else
                        response.EnsureSuccessStatusCode();
                }
                else
                    throw new InvalidOperationException(message);
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var jsonText = await reader.ReadToEndAsync();
            var responseJson = JObject.Parse(jsonText);
            var content = responseJson["choices"]?.FirstOrDefault()?["message"]?["content"]?.Value<string>() ?? string.Empty;
            return content;
        }

        static HttpRequestMessage CreateRequest(string systemPrompt, string text, Bitmap? image)
        {
            var url = "https://api.x.ai/v1/chat/completions";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {GrokSettings.Default.ApiKey}");

            var json = CreateJson(systemPrompt, text, image);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Content = content;

            return request;
        }

        static string CreateJson(string systemPrompt, string text, Bitmap? image)
        {
            var imageUrl = GrokSettings.Default.IsSendImageEnabled ? CreateBase64ImageUrl(image) : null;

            var isReasoningModel = GrokModels.FindModel(GrokSettings.Default.RequestSettings.Model)?.IsReasoningOnly ?? false;

            object messages;
            if(imageUrl is not null)
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = (object)systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = (object)new object[]
                        {
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = imageUrl
                                }
                            },
                            new
                            {
                                type = "text",
                                text
                            },
                        }
                    }
                };
            }
            else
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = text
                    }
                };
            }

            var model = string.IsNullOrWhiteSpace(GrokSettings.Default.RequestSettings.Model) ? GrokModels.DefaultModel : GrokSettings.Default.RequestSettings.Model;
            var request = new
            {
                model,
                messages,
                temperature = GrokSettings.Default.RequestSettings.Temperature,
                top_p = GrokSettings.Default.RequestSettings.TopP,
                presence_penalty = isReasoningModel ? (double?)null : GrokSettings.Default.RequestSettings.PresencePenalty,
                frequency_penalty = isReasoningModel ? (double?)null : GrokSettings.Default.RequestSettings.FrequencyPenalty,
            };
            
            var json = Json.Json.GetJsonText(request, new Newtonsoft.Json.JsonSerializerSettings() { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });
            return json;
        }

        static string? CreateBase64ImageUrl(Bitmap? image)
        {
            if (image is null)
                return null;

            var scale = 1.0;
            while (true)
            {
                var scaledImage = Resize(image, scale);
                using var stream = CreateJpegStream(scaledImage);
                //https://docs.x.ai/docs/guides/image-understanding#image-understanding
                //画像サイズは10MiB以下である必要がある
                if (stream.Length > 10 * 1024 * 1024)
                {
                    scale *= 0.9;
                    continue;
                }

                var base64 = Convert.ToBase64String(stream.ToArray());
                var imageUrl = $"data:image/jpeg;base64,{base64}";
                return imageUrl;
            }
        }

        static Bitmap Resize(Bitmap image, double scale)
        {
            if (scale is 1)
                return image;
            return new Bitmap(image, new Size((int)(image.Width * scale), (int)(image.Height * scale)));
        }

        static MemoryStream CreateJpegStream(Bitmap image)
        {
            var stream = new MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            stream.Position = 0;
            return stream;
        }
    }
}
