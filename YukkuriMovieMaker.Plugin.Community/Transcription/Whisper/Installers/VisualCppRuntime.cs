using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers
{
    internal class VisualCppRuntime : InstallerBase
    {
        public static bool IsSupported() => RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86 or Architecture.Arm64;
        public static bool IsInstalled()
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

        public override Task<(string url, string fileName)> GetDownloadUrlAsync(CancellationToken token)
        {
            //https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-microsoft-visual-c-redistributable-version
            var url = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                Architecture.X86 => "https://aka.ms/vs/17/release/vc_redist.x86.exe",
                Architecture.Arm64 => "https://aka.ms/vs/17/release/vc_redist.arm64.exe",
                _ => throw new NotImplementedException()
            };
            return Task.FromResult((url, Path.GetFileName(url)));
        }

        public override string GetInstallerArgs() => "/install /quiet /norestart";

        public override bool IsSuccessExitCode(int exitCode) => exitCode is 0 or 3010;
    }
}
