using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper.Installers
{
    internal abstract class InstallerBase
    {
        public abstract Task<(string url, string fileName)> GetDownloadUrlAsync(CancellationToken token);
        public abstract string GetInstallerArgs();
        public abstract bool IsSuccessExitCode(int exitCode);

        public async Task InstallAsync(ProgressMessage progress, CancellationToken token)
        {
            var (url, fileName) = await GetDownloadUrlAsync(token);
            if (string.IsNullOrEmpty(url))
                throw new InvalidOperationException("URL is null or empty.");
            if(string.IsNullOrEmpty(fileName))
                throw new InvalidOperationException("FileName is null or empty.");

            var installerPath = Path.Combine(AppDirectories.TemporaryDirectory, fileName);
            try
            {
                await Downloader.DownloadAsync(url, installerPath, progress, token);
                progress.Report(-1, string.Format(Texts.InstallingMessage, fileName));
                await RunInstallerAsync(installerPath);

            }
            finally
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);
            }
        }

        private async Task RunInstallerAsync(string installerPath)
        {
            if (!File.Exists(installerPath))
                throw new FileNotFoundException(string.Format(Texts.InstallerNotFoundMessage, installerPath), installerPath);

            var args = GetInstallerArgs();
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                using var process = Process.Start(psi) ?? throw new InvalidOperationException(Texts.InstallerStartFailedMessage);
                await process.WaitForExitAsync();
                var exitCode = process.ExitCode;
                if (!IsSuccessExitCode(exitCode))
                    throw new InvalidOperationException(string.Format(Texts.InstallerExitFailureMessage, exitCode));
            }
            catch (Win32Exception e) when (e.NativeErrorCode is 1223)
            {
                throw new OperationCanceledException();
            }
        }
    }
}
