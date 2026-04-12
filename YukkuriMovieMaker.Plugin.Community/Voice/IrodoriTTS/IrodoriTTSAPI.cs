using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal static class IrodoriTTSAPI
{
    const string DefaultTTSCheckpoint = "Aratako/Irodori-TTS-500M-v2";
    const string DefaultVoiceDesignCheckpoint = "Aratako/Irodori-TTS-500M-v2-VoiceDesign";
    const string DefaultDevice = "cuda";
    const string DefaultPrecision = "fp32";

    public static async Task<bool> HealthCheckAsync(string baseUrl)
    {
        try
        {
            var client = HttpClientFactory.Client;
            using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/gradio_api/openapi.json");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS HealthCheck failed", ex);
            return false;
        }
    }

    public static async Task SynthesizeAsync(
        string baseUrl,
        string text,
        string refFilePath,
        int numSteps,
        double cfgScaleText,
        double cfgScaleSpeaker,
        string outputPath,
        string checkpoint = "")
    {
        // ref-wav を Gradio にアップロード（セキュリティ制約回避）
        var uploadedPath = await UploadFileAsync(baseUrl, refFilePath);

        var ttsCheckpoint = string.IsNullOrWhiteSpace(checkpoint) ? DefaultTTSCheckpoint : checkpoint;

        // 24 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
        //   enable_watermark (hidden State), text, uploaded_audio, num_steps, num_candidates,
        //   seed_raw, cfg_guidance_mode, cfg_scale_text, cfg_scale_speaker, cfg_scale_raw,
        //   cfg_min_t, cfg_max_t, context_kv_cache, truncation_factor_raw,
        //   rescale_k_raw, rescale_sigma_raw, speaker_kv_scale_raw,
        //   speaker_kv_min_t_raw, speaker_kv_max_layers_raw
        var data = new JArray
        {
            ttsCheckpoint,             // checkpoint
            DefaultDevice,             // model_device
            DefaultPrecision,          // model_precision
            DefaultDevice,             // codec_device
            DefaultPrecision,          // codec_precision
            false,                     // enable_watermark (hidden State component)
            text,                      // text
            new JObject { ["path"] = uploadedPath, ["meta"] = new JObject { ["_type"] = "gradio.FileData" } },  // uploaded_audio (ref-wav)
            numSteps,                  // num_steps
            1,                         // num_candidates
            "",                        // seed_raw (empty = random)
            "independent",             // cfg_guidance_mode
            cfgScaleText,              // cfg_scale_text
            cfgScaleSpeaker,           // cfg_scale_speaker
            "",                        // cfg_scale_raw
            0.5,                       // cfg_min_t
            1.0,                       // cfg_max_t
            true,                      // context_kv_cache
            "",                        // truncation_factor_raw
            "",                        // rescale_k_raw
            "",                        // rescale_sigma_raw
            "",                        // speaker_kv_scale_raw
            "",                        // speaker_kv_min_t_raw
            "",                        // speaker_kv_max_layers_raw
        };

        var result = await CallGradioAsync(baseUrl, "_run_generation", data);
        await DownloadFirstAudioResult(result, outputPath);
    }

    public static async Task VoiceDesignAsync(
        string baseUrl,
        string text,
        string caption,
        string seed,
        int numSteps,
        string outputPath,
        string checkpoint = "")
    {
        var vdCheckpoint = string.IsNullOrWhiteSpace(checkpoint) ? DefaultVoiceDesignCheckpoint : checkpoint;

        // gradio_app_voicedesign.py の _run_generation に対応
        // 23 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
        //   enable_watermark (hidden State), text, caption, num_steps, num_candidates,
        //   seed_raw, cfg_guidance_mode, cfg_scale_text, cfg_scale_caption, cfg_scale_raw,
        //   cfg_min_t, cfg_max_t, context_kv_cache, max_text_len_raw, max_caption_len_raw,
        //   truncation_factor_raw, rescale_k_raw, rescale_sigma_raw
        var data = new JArray
        {
            vdCheckpoint,              // checkpoint
            DefaultDevice,             // model_device
            DefaultPrecision,          // model_precision
            DefaultDevice,             // codec_device
            DefaultPrecision,          // codec_precision
            false,                     // enable_watermark (hidden State component)
            text,                      // text
            caption,                   // caption
            numSteps,                  // num_steps
            1,                         // num_candidates
            seed,                      // seed_raw
            "independent",             // cfg_guidance_mode
            2.0,                       // cfg_scale_text (VoiceDesign default)
            4.0,                       // cfg_scale_caption (VoiceDesign default)
            "",                        // cfg_scale_raw
            0.5,                       // cfg_min_t
            1.0,                       // cfg_max_t
            true,                      // context_kv_cache
            "",                        // max_text_len_raw
            "",                        // max_caption_len_raw
            "",                        // truncation_factor_raw
            "",                        // rescale_k_raw
            "",                        // rescale_sigma_raw
        };

        var result = await CallGradioAsync(baseUrl, "_run_generation", data);
        await DownloadFirstAudioResult(result, outputPath);
    }

    static async Task<JArray> CallGradioAsync(string baseUrl, string apiName, JArray data)
    {
        var url = $"{baseUrl.TrimEnd('/')}/gradio_api/call/{apiName}";
        var client = HttpClientFactory.Client;

        // Step 1: POST to submit
        var payload = new JObject { ["data"] = data };
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

        using var postResponse = await client.SendAsync(request);
        postResponse.EnsureSuccessStatusCode();

        var postBody = await postResponse.Content.ReadAsStringAsync();
        var postResult = JObject.Parse(postBody);
        var eventId = postResult["event_id"]?.ToString()
            ?? throw new InvalidOperationException("No event_id in Gradio response.");

        // Step 2: GET SSE stream for result
        var resultUrl = $"{url}/{eventId}";
        using var getResponse = await client.GetAsync(resultUrl, HttpCompletionOption.ResponseHeadersRead);
        getResponse.EnsureSuccessStatusCode();

        using var stream = await getResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? currentEvent = null;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("event: "))
            {
                currentEvent = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ") && currentEvent == "complete")
            {
                var dataStr = line["data: ".Length..];
                return JArray.Parse(dataStr);
            }
            else if (currentEvent == "error" && line.StartsWith("data: "))
            {
                var errorData = line["data: ".Length..];
                throw new InvalidOperationException($"Gradio error: {errorData}");
            }
        }

        throw new InvalidOperationException("Gradio stream ended without a complete event.");
    }

    static async Task<string> UploadFileAsync(string baseUrl, string filePath)
    {
        var url = $"{baseUrl.TrimEnd('/')}/gradio_api/upload";
        var client = HttpClientFactory.Client;

        using var content = new MultipartFormDataContent();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "files", Path.GetFileName(filePath));

        using var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();
        var paths = JArray.Parse(responseText);
        return paths[0]?.ToString()
            ?? throw new InvalidOperationException("Failed to upload file to Gradio.");
    }

    static async Task DownloadFirstAudioResult(JArray result, string outputPath)
    {
        // Gradio returns update objects: {"value": {"url": "...", "path": "..."}, "__type__": "update"}
        // Find the first item with a non-null value containing audio file info
        foreach (var item in result)
        {
            // Extract the actual file data (may be nested in "value" or at top level)
            var fileData = item is JObject obj && obj["__type__"]?.ToString() == "update"
                ? obj["value"] as JObject
                : item as JObject;

            if (fileData == null || fileData.Type == JTokenType.Null)
                continue;

            // Try URL download first
            var url = fileData["url"]?.ToString();
            if (!string.IsNullOrEmpty(url))
            {
                var client = HttpClientFactory.Client;
                using var audioResponse = await client.GetAsync(url);
                audioResponse.EnsureSuccessStatusCode();

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await audioResponse.Content.CopyToAsync(fileStream);
                return;
            }

            // Fallback to local file path
            var path = fileData["path"]?.ToString();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(path, outputPath, overwrite: true);
                return;
            }
        }

        throw new InvalidOperationException("No audio file found in Gradio response.");
    }
}
