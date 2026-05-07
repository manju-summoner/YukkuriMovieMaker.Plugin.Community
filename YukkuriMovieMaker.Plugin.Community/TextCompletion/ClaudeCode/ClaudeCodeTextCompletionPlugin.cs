using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.ClaudeCode
{
    internal class ClaudeCodeTextCompletionPlugin : ITextCompletionPlugin2
    {
        public object? SettingsView => new ClaudeCodeSettingsView();

        public string Name => Texts.ClaudeCode;

        public Task<string> ProcessAsync(string systemPrompt, string text)
            => ProcessAsync(systemPrompt, text, null);

        public async Task<string> ProcessAsync(string systemPrompt, string text, Bitmap? image)
        {
            var settings = ClaudeCodeSettings.Default;
            var prompt = BuildPrompt(systemPrompt, text, settings.IsSendImageEnabled, image, out var imagePath);
            var startInfo = CreateStartInfo(settings);

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

        static ProcessStartInfo CreateStartInfo(ClaudeCodeSettings settings)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude.cmd",
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

            startInfo.ArgumentList.Add("--print");
            startInfo.ArgumentList.Add("--output-format");
            startInfo.ArgumentList.Add("text");
            startInfo.ArgumentList.Add("--no-session-persistence");
            startInfo.ArgumentList.Add("--permission-mode");
            startInfo.ArgumentList.Add("default");
            startInfo.ArgumentList.Add("--effort");
            startInfo.ArgumentList.Add(settings.Effort.ToString().ToLowerInvariant());

            startInfo.ArgumentList.Add("--allowedTools");
            startInfo.ArgumentList.Add("Read");

            if (!string.IsNullOrWhiteSpace(settings.Model))
            {
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(settings.Model);
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                startInfo.Environment["ANTHROPIC_API_KEY"] = settings.ApiKey;

            return startInfo;
        }

        static async Task<string> ExecuteAsync(ProcessStartInfo startInfo, string prompt, int timeoutSeconds)
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException(Texts.ClaudeCodeProcessStartFailedMessage);

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
                throw new TimeoutException(string.Format(Texts.ClaudeCodeTimeoutMessage, timeoutSeconds));
            }

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.Format(Texts.ClaudeCodeProcessFailedMessage, process.ExitCode, string.IsNullOrWhiteSpace(error) ? Texts.NoDetails : error));

            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(Texts.ClaudeCodeEmptyResponseMessage);

            return output;
        }

        static string BuildPrompt(string systemPrompt, string text, bool isSendImageEnabled, Bitmap? image, out string? imagePath)
        {
            imagePath = null;

            var builder = new StringBuilder();
            builder.AppendLine("# システム指示");
            builder.AppendLine(systemPrompt);
            builder.AppendLine();
            builder.AppendLine("# 入力済みテキスト");
            builder.AppendLine(text);

            if (isSendImageEnabled && image is not null)
            {
                imagePath = Path.Combine(Path.GetTempPath(), $"ymm4-claudecode-{Guid.NewGuid():N}.jpg");
                image.Save(imagePath, ImageFormat.Jpeg);
                builder.AppendLine();
                builder.AppendLine("# 現在のフレーム画像");
                builder.AppendLine(imagePath);
            }

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
