using System.Net.Http;
using Newtonsoft.Json.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperUpdateChecker
{
    const string ReleasesApiUrl = "https://api.github.com/repos/ayutaz/piper-plus/releases/latest";

    static readonly SemaphoreSlim Gate = new(1, 1);
    static GitHubReleaseInfo? cachedRelease;
    static bool isCached;

    public static GitHubReleaseInfo? CachedRelease => cachedRelease;

    public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (isCached)
                return cachedRelease;

            var result = await FetchLatestAsync(cancellationToken);

            if (result is not null)
            {
                cachedRelease = result;
                isCached = true;
            }

            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task InvalidateCacheAsync()
    {
        await Gate.WaitAsync();
        try
        {
            cachedRelease = null;
            isCached = false;
        }
        finally
        {
            Gate.Release();
        }
    }

    static async Task<GitHubReleaseInfo?> FetchLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = HttpClientFactory.Client;
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.UserAgent.ParseAdd("YukkuriMovieMaker-PiperPlus/1.0");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var obj = JObject.Parse(json);
            var tag = obj["tag_name"]?.Value<string>();
            var url = obj["html_url"]?.Value<string>();

            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(url))
                return null;

            return new GitHubReleaseInfo(tag, url);
        }
        catch
        {
            return null;
        }
    }
}
