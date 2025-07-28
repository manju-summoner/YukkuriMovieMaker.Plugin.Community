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
            var model = GeminiModels.FindModel(GeminiTextCompletionSettings.Default.Model); 
            
            var modelName = model?.Key ?? GeminiTextCompletionSettings.Default.Model;
            var suffix = (model != null && model.IsBeta) || (model is null && GeminiTextCompletionSettings.Default.IsPreviewModel) ? "beta" : "";
            int? thinkingBudget = 
                model is null && GeminiTextCompletionSettings.Default.IsSkipReasoningProcess ? 0 : 
                model != null && GeminiTextCompletionSettings.Default.IsSkipReasoningProcess ? model.ThinkingBudgetOnSkipReasoning : 
                null;

            var json = CreateJson(systemPrompt, text, image, thinkingBudget);
            var jsonText = json.ToString();
            var jsonContent = new StringContent(jsonText, Encoding.UTF8, "application/json");

            var endpoint = $"https://generativelanguage.googleapis.com/v1{suffix}/models/{modelName}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", GeminiTextCompletionSettings.Default.APIKey);
            request.Content = jsonContent;

            var client = HttpClientFactory.Client;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var responseText = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseText);
            if (!response.IsSuccessStatusCode)
            {
                var token = responseJson["error"]?["message"];
                if (token != null)
                    throw new Exception(token.Value<string>());
            }
            response.EnsureSuccessStatusCode();

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

        private static JObject CreateJson(string systemPrompt, string text, Bitmap? image, int? thinkingBudget)
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
                },
                ["generationConfig"] = new JObject()
                {
                    ["temperature"] = GeminiTextCompletionSettings.Default.Temperature,
                    ["topK"] = GeminiTextCompletionSettings.Default.TopK,
                    ["topP"] = GeminiTextCompletionSettings.Default.TopP,
                },
                ["safety_settings"] = new JArray()
                {
                    new JObject()
                    {
                        ["category"] = "HARM_CATEGORY_HARASSMENT",
                        ["threshold"] = GetString(GeminiTextCompletionSettings.Default.Harassment),
                    },
                    new JObject()
                    {
                        ["category"] = "HARM_CATEGORY_HATE_SPEECH",
                        ["threshold"] = GetString(GeminiTextCompletionSettings.Default.HateSpeech),
                    },
                    new JObject()
                    {
                        ["category"] = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                        ["threshold"] = GetString(GeminiTextCompletionSettings.Default.SexuallyExplicit),
                    },
                    new JObject()
                    {
                        ["category"] = "HARM_CATEGORY_DANGEROUS_CONTENT",
                        ["threshold"] = GetString(GeminiTextCompletionSettings.Default.DangerousContent),
                    },
                    new JObject()
                    {
                        ["category"] = "HARM_CATEGORY_CIVIC_INTEGRITY",
                        ["threshold"] = GetString(GeminiTextCompletionSettings.Default.CivicIntegrity),
                    },
                },
            };
            if(thinkingBudget != null)
            {
                json!["generationConfig"]!["thinkingConfig"] = new JObject()
                {
                    ["thinkingBudget"] = thinkingBudget,
                };
            }
            return json;
        }

        static string GetString(SafetyLevel safetyLevel) => 
            safetyLevel switch 
            { 
                SafetyLevel.BlockNone => "BLOCK_NONE",
                SafetyLevel.BlockFew => "BLOCK_ONLY_HIGH",
                SafetyLevel.BlockSome => "BLOCK_MEDIUM_AND_ABOVE",
                SafetyLevel.BlockMost => "BLOCK_LOW_AND_ABOVE",
                _=>throw new NotImplementedException(),
            };

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
