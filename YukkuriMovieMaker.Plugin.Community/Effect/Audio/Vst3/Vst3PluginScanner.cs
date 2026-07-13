using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal record Vst3EffectPluginInfo(string ModulePath, string ClassId, string Name, string Vendor)
    {
        public string DisplayName => string.IsNullOrEmpty(Vendor) ? Name : $"{Name} ({Vendor})";
    }

    /// <summary>
    /// システムのVST3ディレクトリからエフェクトプラグインを列挙する。
    /// 全モジュールをロードして走査するため初回は時間がかかる。結果はセッション内でキャッシュされる。
    /// </summary>
    internal static class Vst3PluginScanner
    {
        static readonly object lockObject = new();
        static IReadOnlyList<Vst3EffectPluginInfo>? cache;

        /// <summary>
        /// スキャン済みの結果。未スキャンならnull
        /// </summary>
        public static IReadOnlyList<Vst3EffectPluginInfo>? CachedPlugins
        {
            get
            {
                lock (lockObject)
                    return cache;
            }
        }

        /// <summary>
        /// 標準のVST3検索フォルダー（存在しないものも含む）
        /// </summary>
        public static IEnumerable<string> GetDefaultDirectories()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "VST3");
            yield return Path.Combine(AppDirectories.PluginDirectory, "VST3");
        }

        public static IReadOnlyList<Vst3EffectPluginInfo> GetEffectPlugins(bool refresh = false)
        {
            lock (lockObject)
            {
                if (cache is not null && !refresh)
                    return cache;

                var modulePaths = EnumerateModulePaths().ToList();
                var plugins = ScanIsolated(modulePaths) ?? ScanInProcess(modulePaths);
                cache = plugins.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                return cache;
            }
        }

        /// <summary>
        /// スキャナープロセスによる隔離スキャン。壊れたプラグインがあってもYMM4本体は巻き込まれない。
        /// スキャナーEXEが見つからない・起動できない場合はnull
        /// </summary>
        static List<Vst3EffectPluginInfo>? ScanIsolated(List<string> modulePaths)
        {
            var scannerPath = Vst3ScannerProcess.FindScannerPath();
            if (scannerPath is null)
            {
                Log.Default.Write($"{Vst3ScannerProcess.ExeName}が見つからないため、プロセス内でVST3をスキャンします。");
                return null;
            }
            try
            {
                return Vst3ScannerProcess.Scan(scannerPath, modulePaths);
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3スキャナープロセスの実行に失敗したため、プロセス内でVST3をスキャンします。", e);
                return null;
            }
        }

        /// <summary>
        /// プロセス内スキャン（スキャナーEXEが使えない環境向けのフォールバック）。
        /// モジュールを本体プロセスへロードするため、壊れたプラグインのクラッシュには巻き込まれる
        /// </summary>
        static List<Vst3EffectPluginInfo> ScanInProcess(List<string> modulePaths)
        {
            var plugins = new List<Vst3EffectPluginInfo>();
            foreach (var modulePath in modulePaths)
            {
                try
                {
                    using var module = Vst3Module.Open(modulePath);
                    foreach (var classInfo in module.GetAudioModuleClasses())
                    {
                        if (!classInfo.IsEffect)
                            continue;
                        plugins.Add(new Vst3EffectPluginInfo(modulePath, classInfo.ClassId, classInfo.Name, classInfo.Vendor));
                    }
                }
                catch (Exception e)
                {
                    // 壊れたモジュールや非対応アーキテクチャはスキップする
                    Log.Default.Write($"VST3モジュールの走査に失敗しました。path={modulePath}", e);
                }
            }
            return plugins;
        }

        static IEnumerable<string> EnumerateModulePaths()
        {
            var roots = GetDefaultDirectories()
                .Concat(Vst3Settings.Default.AdditionalPluginDirectories)
                .Where(x => !string.IsNullOrWhiteSpace(x));
            foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // .vst3はバンドル形式（フォルダ）と単一ファイルの両方がある。
                // バンドルフォルダはそれ自体をモジュールとして返し、中へは降りない。
                var directories = new Stack<string>();
                directories.Push(root);
                while (directories.Count > 0)
                {
                    var directory = directories.Pop();
                    IEnumerable<string> entries;
                    try
                    {
                        entries = Directory.EnumerateFileSystemEntries(directory);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }
                    foreach (var entry in entries)
                    {
                        var isVst3 = entry.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase);
                        if (Directory.Exists(entry))
                        {
                            if (isVst3)
                                yield return entry;
                            else
                                directories.Push(entry);
                        }
                        else if (isVst3)
                        {
                            yield return entry;
                        }
                    }
                }
            }
        }
    }
}
