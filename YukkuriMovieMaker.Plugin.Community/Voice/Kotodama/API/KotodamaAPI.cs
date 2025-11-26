using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API
{
    internal class KotodamaAPI(string token)
    {
        readonly string token = token;

        //https://spiralai.notion.site/Kotodama-API-28e7fe6ac353805ab242da38119b4f1f?pvs=25#28f7fe6ac35380989d41f1cf761285b4

        public async Task Generate(string text, string speakerId, string decorationId, string filePath)
        {
            var client = HttpClientFactory.Client;

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://tts3.spiral-ai-app.com/api/tts_generate");
            request.Headers.Add("X-API-KEY", token);
            var parameters = new 
            {
                text,
                speaker_id = speakerId,
                decoration_id = decorationId,
                audio_format = "wav",
            };
            var json = Json.Json.GetJsonText(parameters);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = Json.Json.LoadFromText<KotodamaGenerateResponse>(responseJson);
            if (responseData is null)
                return;

            if(responseData.Audios.Length is 1)
            {
                var audioBase64 = responseData.Audios[0];
                var audioBytes = Convert.FromBase64String(audioBase64);
                await File.WriteAllBytesAsync(filePath, audioBytes);
            }
            else
            {
                WavFileWriter? writer = null;
                try
                {
                    foreach (var base64 in responseData.Audios)
                    {
                        var audioBytes = Convert.FromBase64String(base64);
                        using var tempFile = new TemporaryFile();
                        await File.WriteAllBytesAsync(tempFile.FullName, audioBytes);

                        using var source = AudioFileSourceFactory.Create(tempFile.FullName, 0);
                        if (source is null)
                            continue;
                        source.Seek(TimeSpan.Zero);

                        writer ??= new WavFileWriter(filePath, source.Hz, 1, 16, WavFileWriter.WAVE_FORMAT_PCM);

                        var buffer = new float[source.Hz * 2];
                        var pcmBuffer = new short[buffer.Length];
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            var pcmBufferLength = read / 2;
                            for (int i = 0; i < pcmBufferLength; i++)
                                pcmBuffer[i] = (short)(Math.Clamp(buffer[i * 2], -1.0f, 1.0f) * short.MaxValue);
                            writer.Write(pcmBuffer[..pcmBufferLength]);
                        }
                    }
                }
                finally
                {
                    writer?.Dispose();
                }
            }
        }
    }
}
