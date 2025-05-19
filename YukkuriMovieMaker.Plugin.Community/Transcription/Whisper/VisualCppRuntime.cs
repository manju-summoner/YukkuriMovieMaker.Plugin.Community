using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    public static class VisualCppRuntime
    {
        public static async Task<string> DownloadRuntimeAsync(ProgressMessage progress, CancellationToken token)
        {
            var url = GetRuntimeDownloadUrl();
            var fileName = Path.GetFileName(url);
            var filePath = Path.Combine(AppDirectories.TemporaryDirectory, fileName);

            await Downloader.DownloadAsync(
                url,
                filePath,
                progress,
                token);

            return filePath;
        }
        public static async Task RunInstallerAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    string.Format(Texts.InstallerNotFoundMessage, filePath), filePath);

            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = "/install /passive /norestart",
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException(Texts.InstallerStartFailedMessage);

                await process.WaitForExitAsync().ConfigureAwait(false);

                if (process.ExitCode is not 0 and not 3010)
                    throw new InvalidOperationException(
                        string.Format(Texts.InstallerExitFailureMessage, process.ExitCode));
            }
            catch(Win32Exception e) when(e.NativeErrorCode is 1223)
            {
                throw new OperationCanceledException();
            }
        }

        public static string GetRuntimeDownloadUrl()
        {
            //https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-microsoft-visual-c-redistributable-version
            return RuntimeInformation.ProcessArchitecture switch
            { 
                Architecture.X64 => "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                Architecture.X86 => "https://aka.ms/vs/17/release/vc_redist.x86.exe",
                Architecture.Arm64 => "https://aka.ms/vs/17/release/vc_redist.arm64.exe",
                Architecture.Arm => throw new NotSupportedException(),
                _ => throw new NotImplementedException()
            };
        }

        public static bool IsVc2019OrLaterInstalled()
        {
            // VS2019 初版の最小ビルド
            var minimum = new Version(14, 21, 27702, 0);

            string archSubKey;
            RegistryView view;

            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    archSubKey = "x64";
                    view = RegistryView.Registry64;
                    break;

                case Architecture.X86:
                    archSubKey = "x86";
                    view = RegistryView.Registry32;
                    break;

                case Architecture.Arm64:
                    archSubKey = "arm64";
                    view = RegistryView.Registry64;
                    break;

                case Architecture.Arm:
                    archSubKey = "arm";
                    view = RegistryView.Registry32;
                    break;

                default:
                    return false;
            }

            using var baseKey = RegistryKey.OpenBaseKey(
                                    RegistryHive.LocalMachine,
                                    view);

            using var key = baseKey.OpenSubKey(
                                    $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{archSubKey}");

            if (key == null) // サブキー自体がなければ未インストール
                return false;

            // Installed DWORD (1 = installed)
            var installedObj = key.GetValue("Installed");
            if (installedObj is not int installed || installed == 0)
                return false;

            // Version REG_SZ からバージョンを取る
            var verString = key.GetValue("Version") as string;
            if (string.IsNullOrWhiteSpace(verString))
                return false;

            // 先頭の 'v' を落として Version に変換
            if (!Version.TryParse(verString.TrimStart('v', 'V'), out var found))
                return false;

            return found >= minimum;
        }
    }
}
