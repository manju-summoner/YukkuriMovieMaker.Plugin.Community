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
        static readonly TimeSpan BinaryTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// スキャナーEXEの配置フォルダー
        /// </summary>
        internal static string OfxBinaryDirectory => Path.Combine(AppDirectories.ResourceDirectory, "bin", "x64", "ofx");

        /// <summary>
        /// スキャナーEXEのパス。見つからない場合はnull（プロセス内スキャンへフォールバックする）
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
            startInfo.ArgumentList.Add(OfxGpuRenderBackendFactory.HasRegisteredBackend ? "true" : "false");
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"OFXスキャナーを起動できませんでした。path={scannerPath}");
            try
            {
                // stderrは読み捨て、stdinは別タスクで書き込む（同期書き込みはパイプ詰まりでデッドロックする）
                _ = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
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
                // #ENDの前に異常終了したバイナリの不完全な結果を混ぜないよう、バイナリ単位でバッファする
                var pendingPlugins = new List<ScannedPlugin>();
                while (true)
                {
                    var readTask = process.StandardOutput.ReadLineAsync();
                    if (!readTask.Wait(BinaryTimeout))
                    {
                        Kill(process);
                        return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, "バイナリが応答しません");
                    }
                    var line = readTask.Result;
                    if (line is null)
                        break;

                    if (line.StartsWith("#BEGIN\t", StringComparison.Ordinal))
                    {
                        currentBinary = line["#BEGIN\t".Length..];
                        pendingPlugins.Clear();
                        // 子プロセスはstdinの順に処理するため、走査開始したバイナリを取り除く
                        if (remaining.Count > 0 && string.Equals(remaining.Peek(), currentBinary, StringComparison.OrdinalIgnoreCase))
                            remaining.Dequeue();
                    }
                    else if (line.StartsWith("#END\t", StringComparison.Ordinal))
                    {
                        // 同一IDの複数バージョン登録（後方互換用）は最新バージョンだけを一覧に載せる。
                        // 対応可否の判定はプロセス内スキャンと同じ基準（CreatePluginInfo）で行う
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
                if (currentBinary is not null)
                    return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, $"スキャナーが異常終了しました。exitCode={process.ExitCode}");
                if (remaining.Count == 0)
                    return false;
                return SkipCurrentBinary(remaining, currentBinary, hasCompletedAnyBinary, $"スキャナーが異常終了しました。exitCode={process.ExitCode}");
            }
            catch
            {
                Kill(process);
                throw;
            }
        }

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
                // 1件も走査できずに失敗した場合はスキャナー自体の問題。
                // 空の結果を正常なスキャン結果としてキャッシュしないよう、失敗として伝播させる
                throw new InvalidOperationException($"OFXスキャナーが走査を開始できませんでした。reason={reason}");
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
