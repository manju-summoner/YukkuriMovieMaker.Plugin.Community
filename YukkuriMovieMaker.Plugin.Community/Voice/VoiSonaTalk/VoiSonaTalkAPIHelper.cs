using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkAPIHelper
    {
        static readonly VersionTextComparer versionTextComparer = new();
        static async Task<VoiSonaTalkAPI?> CreateAPIAsync()
        {
            var userName = VoiSonaTalkSettings.Default.UserName;
            var password = VoiSonaTalkSettings.Default.Password;
            var port = VoiSonaTalkSettings.Default.Port;
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                return null;

            await LaunchVoiSonaTalkAppAsync();

            return new VoiSonaTalkAPI(userName, password, port);
        }

        #region APIの簡易呼び出し
        public static async Task<VoiceInformation[]?> GetVoicesAsync()
        {
            var api = await CreateAPIAsync();
            if (api is null)
                return null;
            var voiceBases = await api.GetVoiceAsync();
            var infos = new VoiceInformation[voiceBases.Length];
            for (int i = 0; i < voiceBases.Length; i++)
            {
                var baseInfo = voiceBases[i];
                infos[i] = await api.GetVoiceAsync(baseInfo.VoiceName, baseInfo.VoiceVersion);
            }
            return infos;
        }

        public static async Task<SpeechSynthesisInformation?> SpeechSynthesisAsync(SpeechSynthesisRequest request)
        {
            var api = await CreateAPIAsync();
            if (api is null)
                return null;
            var response = await api.RequestSpeechSynthesisAsync(request);
            var requestId = response.Uuid;

            while(true)
            {
                var info = await api.GetSpeechSynthesisAsync(requestId);
                if (info.State is RequestState.Succeeded or RequestState.Failed)
                {
                    await api.DeleteSpeechSynthesisAsync(requestId);
                    return info;
                }
                await Task.Delay(100);
            }
        }

        public static async Task<TextAnalysisInformation?> AnalyzeTextAsync(string text, string language)
        {
            var api = await CreateAPIAsync();
            if (api is null)
                return null;
            var response = await api.RequestTextAnalysisAsync(text, language);
            var requestId = response.Uuid;
            while (true)
            {
                var info = await api.GetTextAnalysisAsync(requestId);
                if (info.State is RequestState.Succeeded or RequestState.Failed)
                {
                    await api.DeleteTextAnalysisAsync(requestId);
                    return info;
                }
                await Task.Delay(100);
            }
        }
        #endregion

        public static async Task UpdateVoicesAsync()
        {
            try
            {
                VoiSonaTalkSettings.Default.Voices = await GetVoicesAsync() ?? [];
            }
            catch
            {

            }
            finally
            {
                VoiSonaTalkSettings.Default.IsVoicesCached = true;
            }
        }

        #region VoiceNameからの情報取得
        public static VoiceInformation[] GetVoicesByVoiceName(string voiceName)
        {
            var voices = VoiSonaTalkSettings.Default.Voices
                .Where(x => x.VoiceName == voiceName)
                .OrderByDescending(x => x.VoiceVersion, versionTextComparer);
            return [..voices];
        }
        public static string[] GetVersionsByVoiceName(string voiceName)
        {
            var versions = 
                GetVoicesByVoiceName(voiceName)
                .Select(x => x.VoiceVersion);
            return [..versions];
        }
        public static string[] GetLanguagesByVoiceName(string voiceName)
        {
            var languages =
                GetVoicesByVoiceName(voiceName)
                .SelectMany(x => x.Languages)
                .Distinct();
            return [..languages];
        }
        public static VoiSonaTalkStyleWeight[] GetStyleWeightsByVoiceName(string voiceName)
        {
            var weights =
                GetVoicesByVoiceName(voiceName)
                .SelectMany(v => v.StyleNames.Zip(v.DefaultStyleWeights, (name, weight) => new VoiSonaTalkStyleWeight() { Name = name, Weight = weight }))
                .DistinctBy(x => x.Name);
            return [..weights];
        }
        #endregion

        static string GetActiveVoiSonaTalkAppPath()
        {
            var path = VoiSonaTalkSettings.Default.AppPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Techno-Speech", "VoiSona Talk", "VoiSona Talk.exe");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Techno-Speech", "VoiSona Talk", "VoiSona Talk.exe");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            return string.Empty;
        }

        static bool IsVoiSonaTalkAppRunning()
        {
            var processes = Process.GetProcessesByName("VoiSona Talk");
            return processes.Length > 0;
        }

        public static async Task LaunchVoiSonaTalkAppAsync()
        {
            var appPath = GetActiveVoiSonaTalkAppPath();
            if (string.IsNullOrEmpty(appPath))
                return;
            if (IsVoiSonaTalkAppRunning())
                return;
            var process = new Process();
            process.StartInfo.FileName = appPath;
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(appPath) ?? string.Empty;
            process.StartInfo.UseShellExecute = true;
            process.Start();

            var timeout = DateTime.Now + TimeSpan.FromSeconds(30);
            while (DateTime.Now < timeout)
            {
                var client = HttpClientFactory.Client;
                try
                {
                    var response = await client.GetAsync($"http://localhost:{VoiSonaTalkSettings.Default.Port}/docs/talk_api.html");
                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch
                {
                    // 無視
                }
                await Task.Delay(1000);
            }
        }
    }
}
