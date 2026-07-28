using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3モジュールのクラス列挙をYukkuriMovieMaker.Vst3Scanner.exe（別プロセス）で行う。
    /// 壊れたプラグインのクラッシュ・ハングからYMM4本体を隔離し、
    /// 問題のあったモジュールはスキップしてスキャンを継続する。
    /// プロトコルはYmm4Vst3Scanner.cpp冒頭のコメントを参照。
    /// </summary>
    internal static class Vst3ScannerProcess
    {
        public const string ExeName = "YukkuriMovieMaker.Vst3Scanner.exe";

        /// <summary>
        /// 1行も出力がないままこの時間が経過したモジュールはハングとみなしてスキップする
        /// </summary>
        static readonly TimeSpan ModuleTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// スキャナーEXEのパス。見つからない場合はnull（プロセス内スキャンへフォールバックする）
        /// </summary>
        public static string? FindScannerPath()
        {
            var path = Path.Combine(Vst3Native.Vst3BinaryDirectory, ExeName);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// モジュール群を走査してエフェクトプラグインを列挙する。
        /// 子プロセスがクラッシュ・ハングした場合は該当モジュールをスキップして再起動し、最後まで走査する
        /// </summary>
        public static List<Vst3EffectPluginInfo> Scan(string scannerPath, IReadOnlyList<string> modulePaths)
        {
            var results = new List<Vst3EffectPluginInfo>();
            var remaining = new Queue<string>(modulePaths);
            var hasCompletedAnyModule = false;
            while (remaining.Count > 0)
            {
                var countBeforeScan = remaining.Count;
                if (!ScanCore(scannerPath, remaining, results, ref hasCompletedAnyModule))
                    break;
                // 1件も消化せず戻ってきた場合は再起動しても同じ結果になるため、部分結果のまま打ち切る（無限ループ防止）。
                // ここで例外にするとプロセス内フォールバックへ進み、原因プラグインを本体へロードして隔離が破れる
                if (remaining.Count == countBeforeScan)
                {
                    Log.Default.Write($"VST3スキャナーが進捗しないため、残り{remaining.Count}件の走査を中断します。");
                    break;
                }
            }
            return results;
        }

        /// <summary>
        /// 子プロセス1回分のスキャン。走査できたモジュールはremainingから取り除く。
        /// 継続すべき（スキップして再起動する）場合はtrueを返す
        /// </summary>
        static bool ScanCore(string scannerPath, Queue<string> remaining, List<Vst3EffectPluginInfo> results, ref bool hasCompletedAnyModule)
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
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"VST3スキャナーを起動できませんでした。path={scannerPath}");
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

                string? currentModule = null;
                // #ENDの前に異常終了したモジュールの不完全なクラスを結果に混ぜないよう、モジュール単位でバッファする
                var pendingClasses = new List<Vst3EffectPluginInfo>();
                while (true)
                {
                    var readTask = process.StandardOutput.ReadLineAsync();
                    if (!readTask.Wait(ModuleTimeout))
                    {
                        Kill(process);
                        return SkipCurrentModule(remaining, currentModule, hasCompletedAnyModule, "モジュールが応答しません");
                    }
                    var line = readTask.Result;
                    if (line is null)
                        break;

                    if (line.StartsWith("#BEGIN\t", StringComparison.Ordinal))
                    {
                        currentModule = line["#BEGIN\t".Length..];
                        pendingClasses.Clear();
                        // 子プロセスはstdinの順に処理するため、走査開始したモジュールを取り除く
                        if (remaining.Count > 0 && string.Equals(remaining.Peek(), currentModule, StringComparison.OrdinalIgnoreCase))
                            remaining.Dequeue();
                    }
                    else if (line.StartsWith("#END\t", StringComparison.Ordinal))
                    {
                        results.AddRange(pendingClasses);
                        pendingClasses.Clear();
                        currentModule = null;
                        hasCompletedAnyModule = true;
                    }
                    else if (line.StartsWith("CLASS\t", StringComparison.Ordinal) && currentModule is not null)
                    {
                        // CLASS <classId> <name> <vendor> <category> <subCategories>
                        var fields = line.Split('\t');
                        if (fields.Length < 6)
                            continue;
                        var classInfo = new Vst3ClassInfo(fields[1], fields[2], fields[3], fields[4], fields[5], string.Empty);
                        if (classInfo.IsAudioModuleClass && classInfo.IsEffect)
                            pendingClasses.Add(new Vst3EffectPluginInfo(currentModule, classInfo.ClassId, classInfo.Name, classInfo.Vendor));
                    }
                    else if (line.StartsWith("#ERROR\t", StringComparison.Ordinal))
                    {
                        // 壊れたモジュールや非対応アーキテクチャはスキップする
                        Log.Default.Write($"VST3モジュールの走査に失敗しました。path={currentModule} error={line["#ERROR\t".Length..]}");
                    }
                    // 他の行（プラグインが標準出力へ書き込んだもの等）は無視する
                }

                if (!process.WaitForExit(5000))
                    Kill(process);
                if (currentModule is not null)
                    return SkipCurrentModule(remaining, currentModule, hasCompletedAnyModule, $"スキャナーが異常終了しました。exitCode={process.ExitCode}");
                if (remaining.Count == 0)
                    return false;
                return SkipCurrentModule(remaining, currentModule, hasCompletedAnyModule, $"スキャナーが異常終了しました。exitCode={process.ExitCode}");
            }
            catch
            {
                Kill(process);
                throw;
            }
        }

        static bool SkipCurrentModule(Queue<string> remaining, string? currentModule, bool hasCompletedAnyModule, string reason)
        {
            if (currentModule is null)
            {
                // モジュールの走査中ではないタイミング（#ENDと次の#BEGINの間）で子プロセスが終了した場合、
                // 疑わしいモジュールが特定できないため残りをそのまま再走査する
                // （進捗しない再起動の打ち切りは Scan 側のガードが行う）
                if (hasCompletedAnyModule)
                {
                    Log.Default.Write($"VST3スキャナーが走査の合間に終了しました。残り{remaining.Count}件を再走査します。reason={reason}");
                    return remaining.Count > 0;
                }
                // 1件も走査できずに失敗した場合はスキャナー自体の問題。
                // 空の結果を正常なスキャン結果としてキャッシュしないよう、失敗として伝播させる
                throw new InvalidOperationException($"VST3スキャナーが走査を開始できませんでした。reason={reason}");
            }
            // 通常は#BEGIN時に取り除かれているが、取り除けていない場合の再起動ループをここで防ぐ
            if (remaining.Count > 0 && string.Equals(remaining.Peek(), currentModule, StringComparison.OrdinalIgnoreCase))
                remaining.Dequeue();
            Log.Default.Write($"VST3モジュールの走査をスキップします。path={currentModule} reason={reason}");
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
