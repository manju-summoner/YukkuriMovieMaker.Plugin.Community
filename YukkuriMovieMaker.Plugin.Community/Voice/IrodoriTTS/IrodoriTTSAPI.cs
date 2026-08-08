using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

/// <summary>
/// Irodori-TTSサーバー（gradio_app.py / gradio_app_voicedesign.py）の _run_generation API世代。
/// サーバー側スクリプトの版によって引数の数・並び・参照音声の形が異なるため、呼び出し前に判別して送り分ける。
/// </summary>
internal enum IrodoriTTSApiFormat
{
    /// <summary>v2タグ（enable_watermark あり。TTS 24引数・VoiceDesign 23引数）</summary>
    V2,
    /// <summary>v3タグ（30引数。参照音声は単一の gr.Audio）</summary>
    V3,
    /// <summary>v4系＝現行main（30引数。参照音声が gr.File(file_count="multiple") になり配列で送る）</summary>
    V4,
}

/// <summary>
/// /gradio_api/info から判別したサーバーのAPI世代と、サーバーが宣言している既定値
/// （チェックポイント・デバイス）。デバイスはサーバーの実行環境依存
/// （CPU版torchでは選択肢が cpu のみになり、cuda を送ると弾かれる）のため、サーバー既定を優先する。
/// 精度は既定が環境依存（choices の先頭）で、採用すると既存ユーザーの合成結果が変わりうるため fp32 固定を維持する。
/// </summary>
internal readonly record struct IrodoriTTSApiInfo(
    IrodoriTTSApiFormat Format,
    string? DefaultCheckpoint = null,
    string? ModelDevice = null,
    string? CodecDevice = null);

internal static class IrodoriTTSAPI
{
    const string V2TTSCheckpoint = "Aratako/Irodori-TTS-500M-v2";
    const string V2VoiceDesignCheckpoint = "Aratako/Irodori-TTS-500M-v2-VoiceDesign";
    const string V3TTSCheckpoint = "Aratako/Irodori-TTS-500M-v3";
    const string V3VoiceDesignCheckpoint = "Aratako/Irodori-TTS-600M-v3-VoiceDesign";
    const string V4Checkpoint = "Aratako/Irodori-TTS-v4-Small";
    const string DefaultDevice = "cuda";
    const string DefaultPrecision = "fp32";

    static readonly TimeSpan detectTimeout = TimeSpan.FromSeconds(10);

    // baseUrl（末尾スラッシュ除去済み）→ 最後に判別できたAPI情報。
    // 一過性のinfo取得失敗で既存ユーザーの合成を壊さないためのフォールバック用
    static readonly ConcurrentDictionary<string, IrodoriTTSApiInfo> infoCache = new();

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

    /// <summary>
    /// /gradio_api/info から _run_generation の引数構成を取得してAPI世代と既定チェックポイントを判別する。
    /// 一時的な失敗で誤った世代を確定させないよう数回リトライし、それでも失敗した場合は
    /// 同じサーバーで前回判別できた情報→それも無ければ現行上流（v4）形式の順でフォールバックする
    /// （infoと合成APIは同一サーバーのため、infoが継続的に取れない状況では合成自体も失敗する見込み）。
    /// </summary>
    internal static async Task<IrodoriTTSApiInfo> DetectApiInfoAsync(string baseUrl)
    {
        const int maxAttempts = 3;
        var key = baseUrl.TrimEnd('/');
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string json;
            try
            {
                var client = HttpClientFactory.Client;
                using var cts = new CancellationTokenSource(detectTimeout);
                json = await client.GetStringAsync($"{key}/gradio_api/info", cts.Token);
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                continue;
            }

            try
            {
                var info = ParseApiInfo(json);
                infoCache[key] = info;
                return info;
            }
            catch (Exception ex)
            {
                // 応答の解析失敗はリトライしても結果が変わらないため即フォールバックへ
                lastError = ex;
                break;
            }
        }

        if (infoCache.TryGetValue(key, out var cached))
        {
            Log.Default.Write($"Irodori-TTS API format detection failed. Using last known format ({cached.Format}).", lastError!);
            // 同一URLでTTS/VoiceDesignアプリを切り替える構成（YMM4管理モード）では、
            // キャッシュ済みの既定チェックポイントが別アプリのものである可能性があるため世代とデバイス系のみ引き継ぐ
            return cached with { DefaultCheckpoint = null };
        }
        Log.Default.Write("Irodori-TTS API format detection failed. Fallback to v4 format.", lastError!);
        return new(IrodoriTTSApiFormat.V4);
    }

    internal static IrodoriTTSApiInfo ParseApiInfo(string infoJson)
    {
        var info = JObject.Parse(infoJson);
        if (info["named_endpoints"] is not JObject namedEndpoints
            || namedEndpoints["/_run_generation"] is not JObject endpoint)
            throw new InvalidOperationException("_run_generation endpoint not found in Gradio API info.");
        if (endpoint["parameters"] is not JArray parameters)
            throw new InvalidOperationException("_run_generation parameters are missing or not an array in Gradio API info.");

        var paramObjects = parameters.OfType<JObject>().ToArray();
        if (paramObjects.Length == 0)
            throw new InvalidOperationException("_run_generation has no parameters in Gradio API info.");

        // Gradioはシグネチャを取れないと param_0, param_1, ... を自動命名するため、
        // 実名（自動命名でない名前）が取れたかどうかで判別方法を切り替える。
        // 全世代共通で存在する checkpoint が実名に含まれない場合は名前が信頼できないとみなす
        var names = paramObjects
            .Select(p => p["parameter_name"]?.ToString() ?? string.Empty)
            .Where(n => n.Length > 0 && !IsAutoGeneratedParameterName(n))
            .ToArray();

        var usePosition = !names.Contains("checkpoint");
        var format = usePosition
            ? DetectFormatFromParameterShape(paramObjects)
            : DetectFormatFromParameterNames(names, paramObjects);

        // 空欄時に使う、サーバーが宣言している既定値
        // （gr.Textbox / gr.Dropdown の value= が /info の parameter_default にそのまま載る。
        //   チェックポイントはローカル運用も含めWeb UIの既定と一致し、
        //   デバイス・精度はサーバーの実行環境（CUDAの有無等）に合った値になる）
        // 引数の並びは全世代共通で checkpoint, model_device, model_precision, codec_device, codec_precision
        // のため、自動命名で名前が信頼できないときに限り位置から拾う
        return new(
            format,
            DefaultCheckpoint: GetServerDefault(paramObjects, "checkpoint", 0, usePosition),
            ModelDevice: GetServerDefault(paramObjects, "model_device", 1, usePosition),
            CodecDevice: GetServerDefault(paramObjects, "codec_device", 3, usePosition));
    }

    static string? GetServerDefault(JObject[] paramObjects, string parameterName, int position, bool usePosition)
    {
        var parameter = paramObjects.FirstOrDefault(p => p["parameter_name"]?.ToString() == parameterName);
        if (parameter is null && usePosition && position < paramObjects.Length)
            parameter = paramObjects[position];
        var token = parameter?["parameter_default"];
        var value = token?.Type == JTokenType.String ? token.ToString() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    static bool IsAutoGeneratedParameterName(string name) =>
        name.Length > "param_".Length
        && name.StartsWith("param_", StringComparison.Ordinal)
        && name["param_".Length..].All(char.IsAsciiDigit);

    static IrodoriTTSApiFormat DetectFormatFromParameterNames(string[] names, JObject[] paramObjects)
    {
        // seconds_raw はv3タグで追加された引数。無ければv2世代
        if (!names.Contains("seconds_raw"))
            return IrodoriTTSApiFormat.V2;

        // VoiceDesignサーバーは参照音声の引数名で判別できる（v3: ref_wav → v4: ref_wavs に改名）
        if (names.Contains("ref_wavs"))
            return IrodoriTTSApiFormat.V4;
        if (names.Contains("ref_wav"))
            return IrodoriTTSApiFormat.V3;

        // 通常TTSサーバーは引数名が同一なので、uploaded_audio のコンポーネント型で判別する
        // （v3: gr.Audio＝単一ファイル → v4: gr.File(file_count="multiple")＝配列）
        var uploadedAudio = paramObjects.FirstOrDefault(p => p["parameter_name"]?.ToString() == "uploaded_audio");
        if (HasComponent(uploadedAudio, "Audio"))
            return IrodoriTTSApiFormat.V3;
        return IrodoriTTSApiFormat.V4;
    }

    static IrodoriTTSApiFormat DetectFormatFromParameterShape(JObject[] paramObjects)
    {
        // 引数の実名が取れないサーバー向けのフォールバック判別。
        // 引数の個数はv2世代（hidden Stateを除き22〜23個）とv3以降（30個）で明確に分かれる
        if (paramObjects.Length < 30)
            return IrodoriTTSApiFormat.V2;

        // v3世代は参照音声にAudioコンポーネントを使う（v4はFileに変更された）
        return paramObjects.Any(p => HasComponent(p, "Audio"))
            ? IrodoriTTSApiFormat.V3
            : IrodoriTTSApiFormat.V4;
    }

    static bool HasComponent(JObject? parameter, string component) =>
        string.Equals(parameter?["component"]?.ToString(), component, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// チェックポイント欄が空のときに使うチェックポイントを解決する。
    /// ユーザー指定 → サーバーが宣言している既定 → 世代別の上流フォールバック値
    /// （ローカルチェックポイントが無いときに各世代の gradio スクリプトが使う値）の優先順。
    /// </summary>
    internal static string ResolveCheckpoint(string checkpoint, IrodoriTTSApiInfo info, bool isVoiceDesign)
    {
        if (!string.IsNullOrWhiteSpace(checkpoint))
            return checkpoint;
        if (!string.IsNullOrWhiteSpace(info.DefaultCheckpoint))
            return info.DefaultCheckpoint;
        return info.Format switch
        {
            IrodoriTTSApiFormat.V2 => isVoiceDesign ? V2VoiceDesignCheckpoint : V2TTSCheckpoint,
            IrodoriTTSApiFormat.V3 => isVoiceDesign ? V3VoiceDesignCheckpoint : V3TTSCheckpoint,
            _ => V4Checkpoint,
        };
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
        var info = await DetectApiInfoAsync(baseUrl);

        // ref-wav を Gradio にアップロード（セキュリティ制約回避）
        var uploadedPath = await UploadFileAsync(baseUrl, refFilePath);
        var uploadedAudio = new JObject { ["path"] = uploadedPath, ["meta"] = new JObject { ["_type"] = "gradio.FileData" } };

        var ttsCheckpoint = ResolveCheckpoint(checkpoint, info, isVoiceDesign: false);
        var data = CreateSynthesizeData(info, ttsCheckpoint, text, uploadedAudio, numSteps, cfgScaleText, cfgScaleSpeaker);

        var result = await CallGradioAsync(baseUrl, "_run_generation", data);
        await DownloadFirstAudioResult(result, outputPath);
    }

    internal static JArray CreateSynthesizeData(
        IrodoriTTSApiInfo info,
        string checkpoint,
        string text,
        JToken uploadedAudio,
        int numSteps,
        double cfgScaleText,
        double cfgScaleSpeaker)
    {
        // デバイスはサーバーが宣言する既定を優先する（CPU版torchのサーバーに cuda を送ると弾かれる）
        var modelDevice = info.ModelDevice ?? DefaultDevice;
        var codecDevice = info.CodecDevice ?? DefaultDevice;

        if (info.Format is IrodoriTTSApiFormat.V2)
        {
            // v2タグの gradio_app.py の _run_generation に対応
            // 24 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
            //   enable_watermark (hidden State), text, uploaded_audio, num_steps, num_candidates,
            //   seed_raw, cfg_guidance_mode, cfg_scale_text, cfg_scale_speaker, cfg_scale_raw,
            //   cfg_min_t, cfg_max_t, context_kv_cache, truncation_factor_raw,
            //   rescale_k_raw, rescale_sigma_raw, speaker_kv_scale_raw,
            //   speaker_kv_min_t_raw, speaker_kv_max_layers_raw
            return
            [
                checkpoint,                // checkpoint
                modelDevice,               // model_device
                DefaultPrecision,          // model_precision
                codecDevice,               // codec_device
                DefaultPrecision,          // codec_precision
                false,                     // enable_watermark (hidden State component)
                text,                      // text
                uploadedAudio,             // uploaded_audio (ref-wav)
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
            ];
        }

        // v3タグ以降の gradio_app.py の _run_generation に対応
        // 30 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
        //   text, uploaded_audio, uploaded_speaker_embedding, speaker_embedding_path_raw,
        //   num_steps, num_candidates, seed_raw, seconds_raw, duration_scale, t_schedule_mode,
        //   sway_coeff, cfg_guidance_mode, cfg_scale_text, cfg_scale_speaker, cfg_scale_raw,
        //   cfg_min_t, cfg_max_t, context_kv_cache, truncation_factor_raw, rescale_k_raw,
        //   rescale_sigma_raw, speaker_kv_scale_raw, speaker_kv_min_t_raw,
        //   speaker_kv_max_layers_raw, lora_adapter_raw
        // v4は uploaded_audio が gr.File(file_count="multiple") のため FileData の配列で送る
        var refAudio = info.Format is IrodoriTTSApiFormat.V4
            ? new JArray(uploadedAudio)
            : uploadedAudio;
        return
        [
            checkpoint,                // checkpoint
            modelDevice,               // model_device
            DefaultPrecision,          // model_precision
            codecDevice,               // codec_device
            DefaultPrecision,          // codec_precision
            text,                      // text
            refAudio,                  // uploaded_audio (ref-wav)
            JValue.CreateNull(),       // uploaded_speaker_embedding
            "",                        // speaker_embedding_path_raw
            numSteps,                  // num_steps
            1,                         // num_candidates
            "",                        // seed_raw (empty = random)
            "",                        // seconds_raw（空欄 = 自動）
            1.0,                       // duration_scale
            "linear",                  // t_schedule_mode
            -1.0,                      // sway_coeff
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
            "",                        // speaker_kv_min_t_raw（空欄 = サーバー側既定。v2形式と挙動を揃える）
            "",                        // speaker_kv_max_layers_raw
            "",                        // lora_adapter_raw
        ];
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
        var info = await DetectApiInfoAsync(baseUrl);

        var vdCheckpoint = ResolveCheckpoint(checkpoint, info, isVoiceDesign: true);
        var data = CreateVoiceDesignData(info, vdCheckpoint, text, caption, seed, numSteps);

        var result = await CallGradioAsync(baseUrl, "_run_generation", data);
        await DownloadFirstAudioResult(result, outputPath);
    }

    internal static JArray CreateVoiceDesignData(
        IrodoriTTSApiInfo info,
        string checkpoint,
        string text,
        string caption,
        string seed,
        int numSteps)
    {
        // デバイスはサーバーが宣言する既定を優先する（CPU版torchのサーバーに cuda を送ると弾かれる）
        var modelDevice = info.ModelDevice ?? DefaultDevice;
        var codecDevice = info.CodecDevice ?? DefaultDevice;

        if (info.Format is IrodoriTTSApiFormat.V2)
        {
            // v2タグの gradio_app_voicedesign.py の _run_generation に対応
            // 23 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
            //   enable_watermark (hidden State), text, caption, num_steps, num_candidates,
            //   seed_raw, cfg_guidance_mode, cfg_scale_text, cfg_scale_caption, cfg_scale_raw,
            //   cfg_min_t, cfg_max_t, context_kv_cache, max_text_len_raw, max_caption_len_raw,
            //   truncation_factor_raw, rescale_k_raw, rescale_sigma_raw
            return
            [
                checkpoint,                // checkpoint
                modelDevice,               // model_device
                DefaultPrecision,          // model_precision
                codecDevice,               // codec_device
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
            ];
        }

        // v3タグ以降の gradio_app_voicedesign.py の _run_generation に対応
        // 30 params: checkpoint, model_device, model_precision, codec_device, codec_precision,
        //   text, caption, ref_wav(v4では ref_wavs), num_steps, num_candidates, seed_raw,
        //   seconds_raw, duration_scale, t_schedule_mode, sway_coeff,
        //   cfg_guidance_mode, cfg_scale_text, cfg_scale_caption, cfg_scale_speaker,
        //   cfg_scale_raw, cfg_min_t, cfg_max_t, context_kv_cache,
        //   speaker_kv_scale_raw, max_text_len_raw, max_caption_len_raw,
        //   truncation_factor_raw, rescale_k_raw, rescale_sigma_raw, lora_adapter_raw
        // 参照音声はVoiceDesignでは使わないため、v3（単一）/v4（複数）とも null でよい
        return
        [
            checkpoint,                // checkpoint
            modelDevice,               // model_device
            DefaultPrecision,          // model_precision
            codecDevice,               // codec_device
            DefaultPrecision,          // codec_precision
            text,                      // text
            caption,                   // caption
            JValue.CreateNull(),       // ref_wav / ref_wavs（VoiceDesignでは参照音声なし）
            numSteps,                  // num_steps
            1,                         // num_candidates
            seed,                      // seed_raw
            "",                        // seconds_raw（空欄 = 自動）
            1.0,                       // duration_scale
            "linear",                  // t_schedule_mode
            -1.0,                      // sway_coeff
            "independent",             // cfg_guidance_mode
            3.0,                       // cfg_scale_text
            4.0,                       // cfg_scale_caption
            5.0,                       // cfg_scale_speaker
            "",                        // cfg_scale_raw
            0.5,                       // cfg_min_t
            1.0,                       // cfg_max_t
            true,                      // context_kv_cache
            "",                        // speaker_kv_scale_raw
            "",                        // max_text_len_raw
            "",                        // max_caption_len_raw
            "",                        // truncation_factor_raw
            "",                        // rescale_k_raw
            "",                        // rescale_sigma_raw
            "",                        // lora_adapter_raw
        ];
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
