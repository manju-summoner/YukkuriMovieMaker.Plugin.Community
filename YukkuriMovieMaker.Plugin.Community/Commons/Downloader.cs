using System.IO;
using System.Net.Http;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons
{
    class Downloader
    {
        public static async Task DownloadAsync(string url, string filePath, ProgressMessage progress, CancellationToken token)
        {
            var client = HttpClientFactory.Client;
            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            ReportDownloadProgress(progress, fileName, 0, 0);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", $"YukkuriMovieMaker v{AppVersion.Current}");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            try
            {
                await using var srcStream = await response.Content.ReadAsStreamAsync(token);
                await using var destStream = File.Create(filePath, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);

                var bufferSize = 1024 * 256;
                byte[] buffer = new byte[bufferSize];
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var totalBytesRead = 0L;
                var readCount = 0;
                while ((readCount = await srcStream.ReadAsync(buffer, token)) > 0)
                {
                    await destStream.WriteAsync(buffer.AsMemory(0, readCount), token);
                    totalBytesRead += readCount;
                    ReportDownloadProgress(progress, fileName, totalBytes, totalBytesRead);
                }
            }
            catch
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                throw;
            }
        }
        static void ReportDownloadProgress(ProgressMessage progressMessage, string fileName, long totalBytes, long totalBytesRead)
        {
            var message = string.Format(Texts.DownloadingProgressMessage, fileName, totalBytesRead, totalBytes is 0 ? "???" : totalBytes);
            var rate = totalBytes is 0 ? -1 : (double)totalBytesRead / totalBytes;
            progressMessage.Report(rate, message);
        }
    }
}
