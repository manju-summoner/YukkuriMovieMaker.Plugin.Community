using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal static class SevenZipService
    {
        static readonly Lazy<string?> executablePath = new(ResolveExecutablePath);

        public static string? ExecutablePath => executablePath.Value;

        public static bool IsAvailable => ExecutablePath is not null;

        public static async Task CompressAsync(string[] sourcePaths, string destinationDirectory, CancellationToken token = default)
        {
            var exe = ExecutablePath ?? throw new InvalidOperationException("7-Zip が見つかりません。");
            var baseName = BuildArchiveName(sourcePaths);
            var archivePath = ResolveUniqueArchivePath(destinationDirectory, baseName, ".7z");
            var args = BuildCompressArguments(archivePath, sourcePaths);
            await StartProcessAsync(exe, args, token);
        }

        public static async Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken token = default)
        {
            var exe = ExecutablePath ?? throw new InvalidOperationException("7-Zip が見つかりません。");
            var args = $"x \"{EscapeArgument(archivePath)}\" -o\"{EscapeArgument(destinationDirectory)}\" -aou";
            await StartProcessAsync(exe, args, token);
        }

        static string ResolveUniqueArchivePath(string directory, string baseName, string extension)
        {
            var candidate = Path.Combine(directory, baseName + extension);
            if (!File.Exists(candidate)) return candidate;

            for (int i = 1; ; i++)
            {
                candidate = Path.Combine(directory, $"{baseName} ({i}){extension}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        static string? ResolveExecutablePath()
        {
            var candidate = TryReadRegistryPath(@"SOFTWARE\7-Zip", "Path")
                         ?? TryReadRegistryPath(@"SOFTWARE\WOW6432Node\7-Zip", "Path");

            if (candidate is null) return null;

            var guiExe = Path.Combine(candidate, "7zG.exe");
            if (File.Exists(guiExe)) return guiExe;

            var exe = Path.Combine(candidate, "7z.exe");
            return File.Exists(exe) ? exe : null;
        }

        static string? TryReadRegistryPath(string subKey, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                return key?.GetValue(valueName) as string;
            }
            catch
            {
                return null;
            }
        }

        static string BuildArchiveName(string[] sourcePaths)
        {
            if (sourcePaths.Length == 1)
                return Path.GetFileNameWithoutExtension(sourcePaths[0].TrimEnd(Path.DirectorySeparatorChar));
            return "archive";
        }

        static string BuildCompressArguments(string archivePath, string[] sourcePaths)
        {
            var sources = string.Join(" ", Array.ConvertAll(sourcePaths, p => $"\"{EscapeArgument(p)}\""));
            return $"a \"{EscapeArgument(archivePath)}\" {sources}";
        }

        static string EscapeArgument(string value) => value.Replace("\"", "\\\"");

        static async Task StartProcessAsync(string exe, string arguments, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(exe, arguments)
                {
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true
            };

            process.Exited += (s, e) =>
            {
                tcs.TrySetResult(true);
            };

            await using var registration = token.Register(() =>
            {
                tcs.TrySetCanceled(token);
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch { }
            });

            if (process.Start())
            {
                try
                {
                    await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    Log.Default.Write($"外部プロセスの実行がキャンセルされました。: {exe} {arguments}");
                }
            }
        }
    }
}
