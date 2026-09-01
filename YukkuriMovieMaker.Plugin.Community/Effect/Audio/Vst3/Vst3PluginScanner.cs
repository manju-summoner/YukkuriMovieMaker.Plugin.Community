using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal record Vst3EffectPluginInfo(string ModulePath, string ClassId, string Name, string Vendor)
    {
        [JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(Vendor) ? Name : $"{Name} ({Vendor})";
    }

    /// <summary>
    /// システムのVST3ディレクトリからエフェクトプラグインを列挙する。
    /// 全モジュールをロードして走査するため初回は時間がかかる。結果はセッション内とディスクへキャッシュされる。
    /// </summary>
    internal static class Vst3PluginScanner
    {
        static readonly object lockObject = new();
        static volatile IReadOnlyList<Vst3EffectPluginInfo>? cache;
        static IReadOnlyList<Vst3EffectPluginInfo>? incompleteCache;
        static long lastAutomaticScanAttemptTick = -1;
        const long AutomaticScanRetryIntervalMilliseconds = 30_000;
        // Audio Module ClassかつFxというフィルター条件を変えた場合は、保存済み結果を再評価できないため上げること。
        const int PersistentCacheFormatVersion = 1;
        internal static IPersistentPluginScanCacheStorage<Vst3EffectPluginInfo> PersistentCacheStorage { get; set; } = new Vst3ScanCacheSettingsStorage();

        /// <summary>
        /// スキャン済みの結果。未スキャンならnull。
        /// スキャン実行中のUIスレッドからも呼ばれるため、スキャンを囲むロックは取らない
        /// </summary>
        public static IReadOnlyList<Vst3EffectPluginInfo>? CachedPlugins => cache;

        /// <summary>
        /// 標準のVST3検索フォルダー（存在しないものも含む）
        /// </summary>
        public static IEnumerable<string> GetDefaultDirectories()
            => GetDefaultDirectoryInfos().Select(x => x.Path);

        /// <summary>
        /// 標準のVST3検索フォルダー（存在しないものも含む）。
        /// IsUserManagedはYMM4管理下（YMM4が作成してよいフォルダー）かどうか
        /// </summary>
        public static IEnumerable<(string Path, bool IsUserManaged)> GetDefaultDirectoryInfos()
        {
            yield return (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "VST3"), false);
            yield return (Path.Combine(GetUserProgramFilesCommonPath(), "VST3"), false);
            // user\plugin はYMM4用プラグインのフォルダーのため、VST3プラグインは user\resources 配下に置く
            yield return (Path.Combine(AppDirectories.UserResourceDirectory, "vst3"), true);
        }

        static readonly Guid UserProgramFilesCommonFolderId = new("BCBD3057-CA5C-4622-B42D-BC56DB0AE516");
        const uint KnownFolderFlagDontVerify = 0x00004000;

        /// <summary>
        /// ユーザー単位インストールの標準フォルダー（FOLDERID_UserProgramFilesCommon。通常 %LOCALAPPDATA%\Programs\Common）
        /// </summary>
        static string GetUserProgramFilesCommonPath()
        {
            var pathPtr = IntPtr.Zero;
            try
            {
                if (SHGetKnownFolderPath(UserProgramFilesCommonFolderId, KnownFolderFlagDontVerify, IntPtr.Zero, out pathPtr) == 0
                    && Marshal.PtrToStringUni(pathPtr) is { Length: > 0 } path)
                    return path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Common");
        }

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        public static IReadOnlyList<Vst3EffectPluginInfo> GetEffectPlugins(bool refresh = false)
        {
            var scannerPath = new Lazy<string?>(Vst3ScannerProcess.FindScannerPath);
            return GetEffectPlugins(
                refresh,
                EnumerateModules,
                PersistentCacheStorage,
                modulePaths => ScanIsolatedDetailed(scannerPath.Value, modulePaths),
                () => Vst3ScannerProcess.GetEnvironmentFingerprint(scannerPath.Value));
        }

        internal static IReadOnlyList<Vst3EffectPluginInfo> GetEffectPlugins(
            bool refresh,
            Func<PluginModuleEnumerationResult> enumerateModules,
            IPersistentPluginScanCacheStorage<Vst3EffectPluginInfo> persistentCacheStorage,
            Func<IReadOnlyList<string>, PluginModuleScanResult<Vst3EffectPluginInfo>?> scan,
            Func<string?> getEnvironmentFingerprint)
        {
            lock (lockObject)
            {
                if (cache is not null && !refresh)
                    return cache;
                var now = Environment.TickCount64;
                if (!refresh
                    && incompleteCache is not null
                    && lastAutomaticScanAttemptTick >= 0
                    && now - lastAutomaticScanAttemptTick < AutomaticScanRetryIntervalMilliseconds)
                    return incompleteCache;

                var result = PersistentPluginScanCache.Scan(
                    refresh,
                    enumerateModules(),
                    persistentCacheStorage,
                    PersistentCacheFormatVersion,
                    "VST3モジュール",
                    scan,
                    x => x.ModulePath,
                    (x, path) => x with { ModulePath = path },
                    IsValidPlugin,
                    getEnvironmentFingerprint: getEnvironmentFingerprint);
                if (!result.IsComplete)
                {
                    // 明示的な再走査の後は、直前の自動失敗による抑止時刻を残さない（次の自動呼び出しを妨げない）
                    lastAutomaticScanAttemptTick = refresh ? -1L : Environment.TickCount64;
                    if (cache is not null)
                        return cache;
                    incompleteCache = result.Plugins
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return incompleteCache;
                }
                cache = result.Plugins.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                incompleteCache = null;
                return cache;
            }
        }

        /// <summary>
        /// スキャンせずに、現時点で分かっているプラグイン一覧を返す。
        /// このセッションでスキャンが完了していればその結果、そうでなければ前回までに保存した結果
        /// （フォルダー列挙もスキャナー起動も行わないため、プラグインの増減は更新ボタンによる再走査まで反映されない）。
        /// 失敗した再走査の部分結果は保存結果より情報が少ない（スキャナー起動失敗時は空になる）ため、
        /// 完了した結果が無い間は常に保存結果へ戻る。スキャン実行中に呼ばれた場合はその完了を待つ
        /// </summary>
        public static IReadOnlyList<Vst3EffectPluginInfo> GetKnownPlugins()
            => GetKnownPlugins(PersistentCacheStorage);

        internal static IReadOnlyList<Vst3EffectPluginInfo> GetKnownPlugins(
            IPersistentPluginScanCacheStorage<Vst3EffectPluginInfo> persistentCacheStorage)
        {
            lock (lockObject)
            {
                if (cache is not null)
                    return cache;
                return PersistentPluginScanCache.LoadPersistedPlugins(
                        persistentCacheStorage,
                        PersistentCacheFormatVersion,
                        "VST3モジュール",
                        (x, path) => x with { ModulePath = path },
                        IsValidPlugin)
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        static bool IsValidPlugin(Vst3EffectPluginInfo? x)
            => x is not null
                && x.ModulePath is not null
                && x.ClassId is not null
                && x.Name is not null
                && x.Vendor is not null;

        /// <summary>
        /// スキャナープロセスによる隔離スキャン。壊れたプラグインがあってもYMM4本体は巻き込まれない。
        /// スキャナーEXEが見つからない・起動できない場合は失敗（null）を返し、呼び出し側はキャッシュしない。
        /// ユーザーのVST3モジュールを本体プロセスへロードするフォールバックは行わない。
        /// </summary>
        internal static PluginModuleScanResult<Vst3EffectPluginInfo>? ScanIsolatedDetailed(string? scannerPath, IReadOnlyList<string> modulePaths)
        {
            if (scannerPath is null)
            {
                Log.Default.Write($"{Vst3ScannerProcess.ExeName}が見つからないため、VST3スキャンを中止します。");
                return null;
            }
            try
            {
                return Vst3ScannerProcess.ScanDetailed(scannerPath, modulePaths);
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3スキャナープロセスの実行に失敗したため、VST3スキャンを中止します。", e);
                return null;
            }
        }

        static PluginModuleEnumerationResult EnumerateModules()
        {
            var roots = PersistentPluginScanCache.NormalizePaths(
                GetDefaultDirectories().Concat(Vst3Settings.Default.AdditionalPluginDirectories),
                "VST3検索フォルダー");
            var existingRoots = roots.Where(Directory.Exists).ToArray();
            var rootsWithTransientErrors = new List<string>();
            var rootsWithPermanentErrors = new List<string>();
            var modulePaths = PersistentPluginScanCache.NormalizePaths(
                EnumerateModulePaths(existingRoots, rootsWithTransientErrors, rootsWithPermanentErrors),
                "VST3モジュール");
            return new PluginModuleEnumerationResult(modulePaths, roots)
            {
                RootsWithTransientEnumerationErrors = rootsWithTransientErrors,
                RootsWithPermanentEnumerationErrors = rootsWithPermanentErrors,
            };
        }

        internal static IEnumerable<string> EnumerateModulePaths(IEnumerable<string> roots)
            => EnumerateModulePaths(roots, null);

        internal static IReadOnlyList<string> EnumerateModulePaths(
            IEnumerable<string> roots,
            ICollection<string>? rootsWithTransientErrors,
            ICollection<string>? rootsWithPermanentErrors = null)
        {
            var results = new List<string>();
            foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // .vst3はバンドル形式（フォルダ）と単一ファイルの両方がある。
                // バンドルフォルダはそれ自体をモジュールとして返し、中へは降りない。
                var hasTransientEnumerationError = false;
                var hasPermanentEnumerationError = false;
                var directories = new Stack<string>();
                directories.Push(root);
                while (directories.Count > 0)
                {
                    var directory = directories.Pop();
                    string[] entries;
                    try
                    {
                        // 遅延列挙のMoveNextでも例外が出るため、try内で実体化して漏れなく捕捉する
                        entries = [.. Directory.EnumerateFileSystemEntries(directory)];
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        RecordEnumerationError(e, ref hasTransientEnumerationError, ref hasPermanentEnumerationError);
                        continue;
                    }
                    foreach (var entry in entries)
                    {
                        var isVst3 = entry.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase);
                        // Directory.Existsはアクセス拒否・一時的なIO障害で例外を投げずfalseを返すため、
                        // 例外を検出できる属性取得でファイル/フォルダーを分類する
                        FileAttributes attributes;
                        try
                        {
                            attributes = File.GetAttributes(entry);
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                        {
                            RecordEnumerationError(e, ref hasTransientEnumerationError, ref hasPermanentEnumerationError);
                            continue;
                        }
                        if (attributes.HasFlag(FileAttributes.Directory))
                        {
                            if (isVst3)
                                results.Add(entry);
                            // ジャンクション・シンボリックリンクは辿らない（親を指すリンクによる無限ループ防止）
                            else if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                                directories.Push(entry);
                        }
                        else if (isVst3)
                        {
                            results.Add(entry);
                        }
                    }
                }
                if (hasTransientEnumerationError)
                    rootsWithTransientErrors?.Add(root);
                if (hasPermanentEnumerationError)
                    rootsWithPermanentErrors?.Add(root);
            }
            return results;
        }

        static void RecordEnumerationError(Exception exception, ref bool hasTransientError, ref bool hasPermanentError)
        {
            switch (PersistentPluginScanCache.ClassifyEnumerationException(exception))
            {
                case PluginModuleEnumerationErrorKind.Permanent:
                    hasPermanentError = true;
                    break;
                case PluginModuleEnumerationErrorKind.Transient:
                    hasTransientError = true;
                    break;
            }
        }
    }
}
