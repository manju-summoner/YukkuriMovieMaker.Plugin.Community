using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using YamlDotNet.RepresentationModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers
{
    internal class CUDAToolkit : InstallerBase
    {
        public static bool IsSupported(GpuVendorDetector.GpuVendor vendor) => vendor is GpuVendorDetector.GpuVendor.Nvidia && Environment.Is64BitOperatingSystem;
        public static bool IsInstalled()=> 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUDA_PATH")) ||
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cudart64_120.dll"));

        public override async Task<(string url, string fileName)> GetDownloadUrlAsync(CancellationToken token)
        {
            var version = await GetVersionAsync(token);
            var installerUrl = await GetInstallerUrlAsync(version, token);
            var fileName = Path.GetFileName(installerUrl);

            return (installerUrl, fileName);
        }

        static async Task<string> GetVersionAsync(CancellationToken token)
        {
            const string versionListUrl = "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/n/Nvidia/CUDA";
            var client = HttpClientFactory.Client;

            using var request = new HttpRequestMessage(HttpMethod.Get, versionListUrl);
            request.Headers.Add("User-Agent", $"YukkuriMovieMaker4 / {AppVersion.Current}");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(token);
            var json = JArray.Parse(content);
            var version = json
                .Where(x => x["type"]?.ToString() == "dir")
                .Select(x => (x["name"]?.ToString()))
                .Select(name => Version.TryParse(name, out var version) ? (version, name) : (null, name))
                .Where(x => x.version != null)
                .OfType<(Version version, string name)>()
                .OrderByDescending(x => x.version)
                .Select(x => x.name)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(version))
                throw new InvalidOperationException("CUDA version not found.");
            return version;
        }
        static async Task<string> GetInstallerUrlAsync(string version, CancellationToken token)
        {
            var manifestUrl = $"https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/n/Nvidia/CUDA/{version}/Nvidia.CUDA.installer.yaml";
            var client = HttpClientFactory.Client;

            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            request.Headers.Add("User-Agent", $"YukkuriMovieMaker4 / {AppVersion.Current}");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(token);
            var yaml = new YamlStream();
            using var reader = new StringReader(content);
            yaml.Load(reader);
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var installers = (YamlSequenceNode)root.Children[new YamlScalarNode("Installers")];
            var url = installers
                .OfType<YamlMappingNode>()
                .FirstOrDefault(n => string.Equals(n.Children[new YamlScalarNode("Architecture")]?.ToString(), "x64", StringComparison.OrdinalIgnoreCase))
                ?.Children[new YamlScalarNode("InstallerUrl")]
                ?.ToString();
            if(string.IsNullOrEmpty(url))
                throw new InvalidOperationException("CUDA installer URL not found.");

            return url;
        }

        public override string GetInstallerArgs() => "-y -gm2 -s -n";

        public override bool IsSuccessExitCode(int exitCode) => exitCode is 0;
    }
}
