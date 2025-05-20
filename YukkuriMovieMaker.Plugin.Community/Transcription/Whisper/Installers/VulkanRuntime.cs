using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using YamlDotNet.RepresentationModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers
{
    internal sealed class VulkanRuntime : InstallerBase
    {
        public static bool IsSupported(GpuVendorDetector.GpuVendor vendor) =>
            vendor is GpuVendorDetector.GpuVendor.Nvidia or GpuVendorDetector.GpuVendor.Amd or  GpuVendorDetector.GpuVendor.Intel 
            && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;

        public static bool IsInstalled() => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vulkan-1.dll"));

        public override async Task<(string url, string fileName)> GetDownloadUrlAsync(CancellationToken token)
        {
            var version = await GetVersionAsync(token);
            var installerUrl = await GetInstallerUrlAsync(version, token);
            var fileName = Path.GetFileName(installerUrl);
            return (installerUrl, fileName);
        }

        private static async Task<string> GetVersionAsync(CancellationToken token)
        {
            const string listUrl =
                "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/k/KhronosGroup/VulkanRT";

            var client = HttpClientFactory.Client;
            using var req = new HttpRequestMessage(HttpMethod.Get, listUrl);
            req.Headers.Add("User-Agent", $"YukkuriMovieMaker4 / {AppVersion.Current}");

            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            res.EnsureSuccessStatusCode();

            var json = JArray.Parse(await res.Content.ReadAsStringAsync(token));
            var latest = json
                .Where(x => x["type"]?.ToString() == "dir")          // フォルダのみ
                .Select(x => x["name"]?.ToString())
                .Select(s => Version.TryParse(s, out var v) ? (v, s) : (null, s))
                .Where(x => x.v != null)
                .OfType<(Version v, string s)>()
                .OrderByDescending(x => x.v)
                .Select(x => x.s)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(latest))
                throw new InvalidOperationException("Vulkan Runtime version not found.");

            return latest;
        }

        private static async Task<string> GetInstallerUrlAsync(string version, CancellationToken token)
        {
            var yamlUrl =
                $"https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/k/KhronosGroup/VulkanRT/{version}/KhronosGroup.VulkanRT.installer.yaml";

            var client = HttpClientFactory.Client;
            using var req = new HttpRequestMessage(HttpMethod.Get, yamlUrl);
            req.Headers.Add("User-Agent", $"YukkuriMovieMaker4 / {AppVersion.Current}");

            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            res.EnsureSuccessStatusCode();

            var yaml = new YamlStream();
            yaml.Load(new StringReader(await res.Content.ReadAsStringAsync(token)));

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var installers = (YamlSequenceNode)root.Children[new YamlScalarNode("Installers")];

            var architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => throw new PlatformNotSupportedException("Vulkan Runtime is only supported on x64 and arm64 architectures.")
            };

            var url = installers
                .OfType<YamlMappingNode>()
                .FirstOrDefault(n =>
                    string.Equals(
                        n.Children[new YamlScalarNode("Architecture")]?.ToString(),
                        architecture,
                        StringComparison.OrdinalIgnoreCase))
                ?.Children[new YamlScalarNode("InstallerUrl")]?
                .ToString();

            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("Vulkan Runtime installer URL not found.");

            return url;
        }

        /// <summary>Vulkan Runtime インストーラのサイレントスイッチ。</summary>
        public override string GetInstallerArgs() => "/S";

        public override bool IsSuccessExitCode(int exitCode) => exitCode == 0;
    }
}
