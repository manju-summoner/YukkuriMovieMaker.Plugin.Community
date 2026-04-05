using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.CodexCLI
{
    internal class CodexCLITextCompletionPlugin : ITextCompletionPlugin2
    {
        public object? SettingsView => new CodexCLISettingsView();

        public string Name => Texts.CodexCLI;

        public Task<string> ProcessAsync(string systemPrompt, string text)
            => ProcessAsync(systemPrompt, text, null);

        public async Task<string> ProcessAsync(string systemPrompt, string text, Bitmap? image)
        {
            var settings = CodexCLISettings.Default;
            var prompt = BuildPrompt(systemPrompt, text);
            var startInfo = CreateStartInfo(settings, prompt, out var imagePath, image);

            try
            {
                return await ExecuteAsync(startInfo, prompt, settings.TimeoutSeconds);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    File.Delete(imagePath);
            }
        }

        static ProcessStartInfo CreateStartInfo(CodexCLISettings settings, string prompt, out string? imagePath, Bitmap? image)
        {
            imagePath = null;

            var startInfo = new ProcessStartInfo
            {
                FileName = "codex.cmd",
                WorkingDirectory = Path.GetTempPath(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--skip-git-repo-check");
            startInfo.ArgumentList.Add("--sandbox");
            startInfo.ArgumentList.Add("read-only");

            if (!string.IsNullOrWhiteSpace(settings.Model))
            {
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(settings.Model);
            }

            if (settings.IsSendImageEnabled && image is not null)
            {
                imagePath = Path.Combine(Path.GetTempPath(), $"ymm4-codexcli-{Guid.NewGuid():N}.jpg");
                image.Save(imagePath, ImageFormat.Jpeg);
                startInfo.ArgumentList.Add("--image");
                startInfo.ArgumentList.Add(imagePath);
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                startInfo.Environment["CODEX_API_KEY"] = settings.ApiKey;

            return startInfo;
        }

        static async Task<string> ExecuteAsync(ProcessStartInfo startInfo, string prompt, int timeoutSeconds)
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException(Texts.CodexCLIProcessStartFailedMessage);

            await process.StandardInput.WriteAsync(prompt);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw new TimeoutException(string.Format(Texts.CodexCLITimeoutMessage, timeoutSeconds));
            }

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.Format(Texts.CodexCLIProcessFailedMessage, process.ExitCode, string.IsNullOrWhiteSpace(error) ? Texts.NoDetails : error));

            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(Texts.CodexCLIEmptyResponseMessage);

            return output;
        }

        static string BuildPrompt(string systemPrompt, string text)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# システム指示");
            builder.AppendLine(systemPrompt);
            builder.AppendLine();
            builder.AppendLine("# 入力済みテキスト");
            builder.AppendLine(text);
            return builder.ToString();
        }

        static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
            }
        }
    }
}
