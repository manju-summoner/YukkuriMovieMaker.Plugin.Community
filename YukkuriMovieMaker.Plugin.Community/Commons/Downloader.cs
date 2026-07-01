using System.IO;
using System.Net.Http;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons
{
    class Downloader
    {
        public static async Task DownloadAsync(string url, string filePath, ProgressMessage progress, CancellationToken token)
        {
            var fileName = Path.GetFileName(filePath);
            ReportDownloadProgress(progress, fileName, 0, 0);

            await RetryDownloader.DownloadFileAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", $"YukkuriMovieMaker v{AppVersion.Current}");
                    return request;
                },
                filePath,
                (read, total) => ReportDownloadProgress(progress, fileName, total, read),
                token: token);
        }
        static void ReportDownloadProgress(ProgressMessage progressMessage, string fileName, long totalBytes, long totalBytesRead)
        {
            var message = string.Format(Texts.DownloadingProgressMessage, fileName, totalBytesRead, totalBytes is 0 ? "???" : totalBytes);
            var rate = totalBytes is 0 ? -1 : (double)totalBytesRead / totalBytes;
            progressMessage.Report(rate, message);
        }
    }
}
