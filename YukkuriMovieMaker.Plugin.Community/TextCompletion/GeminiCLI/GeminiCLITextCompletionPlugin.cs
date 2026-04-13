using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GeminiCLI
{
    internal class GeminiCLITextCompletionPlugin : ITextCompletionPlugin2
    {
        public object? SettingsView => new GeminiCLISettingsView();

        public string Name => Texts.GeminiCLI;

        public Task<string> ProcessAsync(string systemPrompt, string text)
            => ProcessAsync(systemPrompt, text, null);

        public async Task<string> ProcessAsync(string systemPrompt, string text, Bitmap? image)
        {
            var settings = GeminiCLISettings.Default;
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

        static ProcessStartInfo CreateStartInfo(GeminiCLISettings settings)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gemini.cmd",
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

            startInfo.ArgumentList.Add("--output-format");
            startInfo.ArgumentList.Add("text");

            if (!string.IsNullOrWhiteSpace(settings.Model))
            {
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(settings.Model);
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                startInfo.Environment["GEMINI_API_KEY"] = settings.ApiKey;

            return startInfo;
        }

        static async Task<string> ExecuteAsync(ProcessStartInfo startInfo, string prompt, int timeoutSeconds)
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException(Texts.GeminiCLIProcessStartFailedMessage);

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
                throw new TimeoutException(string.Format(Texts.GeminiCLITimeoutMessage, timeoutSeconds));
            }

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.Format(Texts.GeminiCLIProcessFailedMessage, process.ExitCode, string.IsNullOrWhiteSpace(error) ? Texts.NoDetails : error));

            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(Texts.GeminiCLIEmptyResponseMessage);

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
                imagePath = Path.Combine(Path.GetTempPath(), $"ymm4-geminicli-{Guid.NewGuid():N}.jpg");
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
