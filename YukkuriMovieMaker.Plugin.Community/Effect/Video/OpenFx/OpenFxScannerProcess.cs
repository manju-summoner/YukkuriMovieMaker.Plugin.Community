using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXバイナリの走査をYukkuriMovieMaker.OfxScanner.exe（ネイティブの別プロセス）で行う。
    /// 壊れたプラグインのクラッシュ・ハングからYMM4本体を隔離し、
    /// 問題のあったバイナリはスキップしてスキャンを継続する。
    /// プロトコルは Ymm4OfxScanner.cpp 冒頭のコメントを参照
    /// （Vst3ScannerProcessと同じ行ベースの流儀。子は生のdescribe結果を返し、
    /// 対応可否の判定は親側の OpenFxPluginScanner.CreatePluginInfo で行う）。
    /// </summary>
    internal static class OpenFxScannerProcess
    {
        /// <summary>子プロセスが返した1プラグイン分の生のdescribe結果</summary>
        sealed record ScannedPlugin(
            string Identifier,
            uint VersionMajor,
            uint VersionMinor,
            string Label,
            string Grouping,
            string[] SupportedContexts,
            string[] SupportedPixelDepths,
            bool IsSingleInstance,
            bool NeedsTemporalClipAccess,
            OpenFxGpuSupport GpuSupport);

        public const string ExeName = "YukkuriMovieMaker.OfxScanner.exe";

        /// <summary>
        /// 1行も出力がないままこの時間が経過したバイナリはハングとみなしてスキップする
        /// </summary>
        static readonly TimeSpan OutputLineTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 進捗がまだないバイナリへ与える初期予算。
        /// </summary>
        static readonly TimeSpan BinaryInitialTimeout = TimeSpan.FromTicks(OutputLineTimeout.Ticks * 4);

        /// <summary>
        /// PLUGIN行1件ごとの延長幅。約180件を含むopenfx-miscでも初回初期化込みで十分な予算になるよう2秒とする。
        /// stdoutの雑多な出力では延長せず、実プラグインの記述完了だけを進捗として扱う。
        /// </summary>
        static readonly TimeSpan BinaryProgressExtension = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 出力を続けるだけの異常な走査を有限時間で止めるための絶対上限。
        /// </summary>
        static readonly TimeSpan BinaryMaximumTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// スキャナーEXEの配置フォルダー
        /// </summary>
        internal static string OfxBinaryDirectory => Path.Combine(AppDirectories.ResourceDirectory, "bin", "x64", "ofx");

        /// <summary>
        /// スキャナーEXEのパス。見つからない場合はnull
        /// </summary>
        public static string? FindScannerPath()
        {
            var path = Path.Combine(OfxBinaryDirectory, ExeName);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// バイナリ群を走査して対応プラグインを列挙する。
        /// 子プロセスがクラッシュ・ハングした場合は該当バイナリをスキップして再起動し、最後まで走査する
        /// </summary>
        public static List<OpenFxPluginInfo> Scan(string scannerPath, IReadOnlyList<string> binaryPaths)
        {
            var results = new List<OpenFxPluginInfo>();
            var remaining = new Queue<string>(binaryPaths);
            var hasCompletedAnyBinary = false;
            while (remaining.Count > 0)
            {
                var countBeforeScan = remaining.Count;
                if (!ScanCore(scannerPath, remaining, results, ref hasCompletedAnyBinary))
                    break;
                // 1件も消化せず戻ってきた場合は再起動しても同じ結果になるため打ち切る（無限ループ防止）
                if (remaining.Count == countBeforeScan)
                {
                    Log.Default.Write($"OFXスキャナーが進捗しないため、残り{remaining.Count}件の走査を中断します。");
                    break;
                }
            }
            return results;
        }

        /// <summary>
        /// 子プロセス1回分のスキャン。走査できたバイナリはremainingから取り除く。
        /// 継続すべき（スキップして再起動する）場合はtrueを返す
        /// </summary>
        static bool ScanCore(string scannerPath, Queue<string> remaining, List<OpenFxPluginInfo> results, ref bool hasCompletedAnyBinary)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = scannerPath,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(scannerPath)) ?? OfxBinaryDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            // ホストのバージョン宣言をC#ホスト（OfxHostDescriptor）と一致させる。
            // バージョンで挙動を変えるプラグインのdescribe結果がスキャンと実行時で食い違わないようにする
            var hostVersion = typeof(OpenFxScannerProcess).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            startInfo.ArgumentList.Add(hostVersion.ToString(4));
            startInfo.ArgumentList.Add(OfxGpuRenderBackendFactory.HasCudaBackend ? "true" : "false");
            startInfo.ArgumentList.Add(OfxGpuRenderBackendFactory.HasOpenClBackend ? "true" : "false");
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"OFXスキャナーを起動できませんでした。path={scannerPath}");
            Task<string>? standardErrorTask = null;
            try
            {
                standardErrorTask = ScannerStandardError.CaptureAsync(process.StandardError.BaseStream);
                // stderrは上限付きで保持しつつ最後まで排出し、stdinは別タスクで書き込む
                // （いずれも同期処理するとパイプ詰まりでデッドロックする）
                var input = string.Join('\n', remaining) + '\n';
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await process.StandardInput.WriteAsync(input);
                        process.StandardInput.Close();
                    }
                    catch (IOException)
                    {
                        // 子プロセスが先に終了した場合。EOF検出側で処理する
                    }
                });

                string? currentBinary = null;
                Stopwatch? currentBinaryStopwatch = null;
                TimeSpan currentBinaryDeadline = BinaryInitialTimeout;
                // #ENDの前に異常終了したバイナリの不完全な結果を混ぜないよう、バイナリ単位でバッファする
                var pendingPlugins = new List<ScannedPlugin>();
                while (true)
                {
                    if (currentBinaryStopwatch is not null && currentBinaryStopwatch.Elapsed >= currentBinaryDeadline)
                    {
                        Kill(process);
                        var reason = ScannerStandardError.AppendToReason("バイナリの総走査時間が上限を超えました", standardErrorTask);
                        return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, reason);
                    }
                    var readTask = process.StandardOutput.ReadLineAsync();
                    var waitTimeout = currentBinaryStopwatch is null
                        ? OutputLineTimeout
                        : TimeSpan.FromTicks(Math.Min(
                            OutputLineTimeout.Ticks,
                            Math.Max(1, (currentBinaryDeadline - currentBinaryStopwatch.Elapsed).Ticks)));
                    if (!readTask.Wait(waitTimeout))
                    {
                        Kill(process);
                        var reason = currentBinaryStopwatch is not null && currentBinaryStopwatch.Elapsed >= currentBinaryDeadline
                            ? "バイナリの総走査時間が上限を超えました"
                            : "バイナリが応答しません";
                        reason = ScannerStandardError.AppendToReason(reason, standardErrorTask);
                        return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, reason);
                    }
                    var line = readTask.GetAwaiter().GetResult();
                    if (line is null)
                        break;

                    if (line.StartsWith("#BEGIN\t", StringComparison.Ordinal))
                    {
                        currentBinary = line["#BEGIN\t".Length..];
                        currentBinaryStopwatch = Stopwatch.StartNew();
                        currentBinaryDeadline = BinaryInitialTimeout;
                        pendingPlugins.Clear();
                        // 子プロセスはstdinの順に処理するため、走査開始したバイナリを取り除く
                        if (remaining.Count > 0 && string.Equals(remaining.Peek(), currentBinary, StringComparison.OrdinalIgnoreCase))
                            remaining.Dequeue();
                    }
                    else if (line.StartsWith("#END\t", StringComparison.Ordinal))
                    {
                        // 同一IDの複数バージョン登録（後方互換用）は最新バージョンだけを一覧に載せる。
                        // 対応可否は親プロセスの共通判定（CreatePluginInfo）で行う
                        if (currentBinary is not null)
                        {
                            foreach (var group in pendingPlugins.GroupBy(p => p.Identifier, StringComparer.OrdinalIgnoreCase))
                            {
                                var latest = group
                                    .OrderByDescending(p => p.VersionMajor)
                                    .ThenByDescending(p => p.VersionMinor)
                                    .First();
                                results.Add(OpenFxPluginScanner.CreatePluginInfo(
                                    currentBinary,
                                    latest.Identifier,
                                    latest.VersionMajor,
                                    latest.VersionMinor,
                                    latest.Label,
                                    latest.Grouping,
                                    latest.SupportedContexts,
                                    latest.SupportedPixelDepths,
                                    latest.IsSingleInstance,
                                    latest.NeedsTemporalClipAccess,
                                    latest.GpuSupport));
                            }
                        }
                        pendingPlugins.Clear();
                        currentBinary = null;
                        currentBinaryStopwatch = null;
                        hasCompletedAnyBinary = true;
                    }
                    else if (line.StartsWith("PLUGIN\t", StringComparison.Ordinal) && currentBinary is not null)
                    {
                        // PLUGIN <id> <verMajor> <verMinor> <label> <grouping> <contexts> <pixelDepths> <singleInstance> <temporalClipAccess>
                        //        <OpenGL> <CUDA> <CUDAStream> <OpenCLRender> <OpenCL> <Metal> <CPU>
                        var fields = line.Split('\t');
                        if (fields.Length < 10
                            || !uint.TryParse(fields[2], out var versionMajor)
                            || !uint.TryParse(fields[3], out var versionMinor))
                            continue;
                        pendingPlugins.Add(new ScannedPlugin(
                            fields[1],
                            versionMajor,
                            versionMinor,
                            fields[4],
                            fields[5],
                            fields[6].Split('|', StringSplitOptions.RemoveEmptyEntries),
                            fields[7].Split('|', StringSplitOptions.RemoveEmptyEntries),
                            fields[8] == "1",
                            fields[9] == "1",
                            fields.Length >= 17
                                ? new OpenFxGpuSupport(fields[10], fields[11], fields[12], fields[13], fields[14], fields[15], fields[16])
                                : OpenFxGpuSupport.Default));
                        currentBinaryDeadline = ExtendBinaryDeadline(currentBinaryDeadline);
                    }
                    else if (line.StartsWith("#ERROR\t", StringComparison.Ordinal))
                    {
                        // 壊れたバイナリや非対応アーキテクチャはスキップする
                        Log.Default.Write($"OFXバイナリの走査に失敗しました。path={currentBinary} error={line["#ERROR\t".Length..]}");
                    }
                    // 他の行（プラグインが標準出力へ書き込んだもの等）は無視する
                }

                if (!process.WaitForExit(5000))
                    Kill(process);
                int? exitCode = process.HasExited ? process.ExitCode : null;
                string GetAbnormalExitReason() => ScannerStandardError.AppendToReason(
                    $"スキャナーが異常終了しました。exitCode={exitCode?.ToString() ?? "(不明)"}",
                    standardErrorTask);
                if (currentBinary is not null)
                    return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, GetAbnormalExitReason());
                if (remaining.Count == 0)
                {
                    if (exitCode is not 0)
                        Log.Default.Write($"OFX{GetAbnormalExitReason()}");
                    return false;
                }
                return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, GetAbnormalExitReason());
            }
            catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
            {
                Kill(process);
                if (e is ScannerFailureException)
                    throw;
                var reason = standardErrorTask is null
                    ? $"{e.Message} stderr=(取得できませんでした)"
                    : ScannerStandardError.AppendToReason(e.Message, standardErrorTask);
                throw new InvalidOperationException(reason, e);
            }
            finally
            {
                if (!process.HasExited)
                    Kill(process);
            }
        }

        internal static TimeSpan ExtendBinaryDeadline(TimeSpan currentDeadline)
            => TimeSpan.FromTicks(Math.Min(
                BinaryMaximumTimeout.Ticks,
                currentDeadline.Ticks + BinaryProgressExtension.Ticks));

        static bool SkipCurrentBinary(Queue<string> remaining, string? currentBinary, bool hasCompletedAnyBinary, string reason)
        {
            if (currentBinary is null)
            {
                // バイナリの走査中ではないタイミング（#ENDと次の#BEGINの間）で子プロセスが終了した場合、
                // 疑わしいバイナリが特定できないため残りをそのまま再走査する
                // （進捗しない再起動の打ち切りは Scan 側のガードが行う）
                if (hasCompletedAnyBinary)
                {
                    Log.Default.Write($"OFXスキャナーが走査の合間に終了しました。残り{remaining.Count}件を再走査します。reason={reason}");
                    return remaining.Count > 0;
                }
                // #BEGINを1度も受信しないまま失敗した場合はスキャナー自体の問題。
                // 空の結果を正常なスキャン結果としてキャッシュしないよう、失敗として伝播させる
                throw new ScannerFailureException($"OFXスキャナーが走査を開始できませんでした。reason={reason}");
            }
            // 通常は#BEGIN時に取り除かれているが、取り除けていない場合の再起動ループをここで防ぐ
            if (remaining.Count > 0 && string.Equals(remaining.Peek(), currentBinary, StringComparison.OrdinalIgnoreCase))
                remaining.Dequeue();
            Log.Default.Write($"OFXバイナリの走査をスキップします。path={currentBinary} reason={reason}");
            return remaining.Count > 0;
        }

        static void Kill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception e) when (e is InvalidOperationException or SystemException)
            {
                // 既に終了している場合等は無視する
            }
        }
    }
}
