using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect
{
    internal enum PluginModuleEnumerationErrorKind
    {
        None,
        Permanent,
        Transient,
    }

    internal sealed record PluginModuleScanResult<TPlugin>(
        IReadOnlyList<TPlugin> Plugins,
        IReadOnlyCollection<string> CompletedModulePaths,
        IReadOnlyCollection<string> SkippedModulePaths)
    {
        /// <summary>
        /// タイムアウトによりスキップしたモジュール。常に<see cref="SkippedModulePaths"/>の部分集合になる。
        /// </summary>
        public IReadOnlyCollection<string> TimeoutSkippedModulePaths { get; init; } = [];
    }

    internal sealed record PluginModuleEnumerationResult(
        IReadOnlyList<string> ModulePaths,
        IReadOnlyCollection<string> ConfiguredRoots)
    {
        /// <summary>
        /// アクセス拒否により一部を読み飛ばした可能性のあるルート。
        /// この配下のエントリは「モジュールが消えた」と断定できないためプルーニングしない
        /// </summary>
        public IReadOnlyCollection<string> RootsWithPermanentEnumerationErrors { get; init; } = [];

        /// <summary>
        /// 一時的なIO障害で一部を読み飛ばした可能性のあるルート。
        /// この配下はプルーニングせず、その回の結果も完了扱いにしない
        /// </summary>
        public IReadOnlyCollection<string> RootsWithTransientEnumerationErrors { get; init; } = [];
    }

    internal sealed record PersistentPluginScanResult<TPlugin>(
        IReadOnlyList<TPlugin> Plugins,
        bool IsComplete);

    internal sealed record PluginModuleSignature(
        long FileCount,
        long TotalSize,
        long LastWriteTimeUtcTicks,
        string? DirectoryContentsHash = null);

    internal sealed class PersistentPluginScanCacheEntry<TPlugin>
    {
        public PluginModuleSignature? Signature { get; set; }
        public bool Failed { get; set; }
        public List<TPlugin> Plugins { get; set; } = [];
    }

    /// <summary>
    /// キャッシュ状態のインメモリDTO。本番でシリアライズされるのは各ScanCacheSettings型
    /// （[JsonProperty]のlowerCamel名）であり、この型のプロパティ名がJSONへ出ることはない。
    /// </summary>
    internal sealed class PersistentPluginScanCacheState<TPlugin>
    {
        public int FormatVersion { get; set; }
        public string? EnvironmentFingerprint { get; set; }
        public Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal interface IPersistentPluginScanCacheStorage<TPlugin>
    {
        PersistentPluginScanCacheState<TPlugin> Load();
        void Save(PersistentPluginScanCacheState<TPlugin> state);
    }

    /// <summary>プラグインスキャン結果の永続化、署名比較、差分マージを行う。</summary>
    internal static class PersistentPluginScanCache
    {
        public static PersistentPluginScanResult<TPlugin> Scan<TPlugin>(
            bool refresh,
            PluginModuleEnumerationResult enumeration,
            IPersistentPluginScanCacheStorage<TPlugin> storage,
            int formatVersion,
            string logName,
            Func<IReadOnlyList<string>, PluginModuleScanResult<TPlugin>?> scan,
            Func<TPlugin, string> getModulePath,
            Func<TPlugin, string, TPlugin> setModulePath,
            Func<TPlugin, bool> isValidPlugin,
            Func<string, string>? getSignaturePath = null,
            Func<string, bool>? includeAdjacentDllsInSignature = null,
            Func<string?>? getEnvironmentFingerprint = null,
            Func<TPlugin, TPlugin, bool>? arePluginsEqual = null)
        {
            arePluginsEqual ??= EqualityComparer<TPlugin>.Default.Equals;
            var normalizedPaths = NormalizePaths(enumeration.ModulePaths, logName);
            var currentPaths = normalizedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var configuredRoots = NormalizePaths(enumeration.ConfiguredRoots, logName);
            var existingRoots = configuredRoots.Where(Directory.Exists).ToArray();
            var stored = LoadOrCreateEmpty(storage, logName);
            // 一時列挙エラーがあった回だけ結果を完了扱いにしない（部分一覧を確定させず、スロットリング後の再列挙に乗せる）
            var permanentErrorRoots = NormalizePaths(enumeration.RootsWithPermanentEnumerationErrors, logName);
            var transientErrorRoots = NormalizePaths(enumeration.RootsWithTransientEnumerationErrors, logName);
            var errorRoots = permanentErrorRoots
                .Concat(transientErrorRoots)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var hasEnumerationErrors = transientErrorRoots.Count > 0;

            // モジュールが1件も無く保存済みエントリも空なら、比較・破棄・保存のいずれも不要。
            // フィンガープリント取得（OFXではCUDA/OpenCLドライバー初期化を伴う）を発生させずに返す
            // （一度プラグインを導入して空になった環境でも効くよう、エントリの有無だけで判定する）
            if (normalizedPaths.Count == 0 && (stored.Entries is null || stored.Entries.Count == 0))
                return new PersistentPluginScanResult<TPlugin>([], !hasEnumerationErrors);

            var environmentFingerprint = NormalizeEnvironmentFingerprint(getEnvironmentFingerprint?.Invoke());
            var isDirty = false;
            // 形式・環境不一致による全破棄は、スキャンで1件も完了しないまま永続化すると
            // 一過性の環境ゆらぎでキャッシュ全損になるため、確定条件を満たすまで保存を遅らせる。
            // 破棄で実際に失うエントリが無い場合（初回起動等）は通常の保存に任せる
            var discardedAllEntries = false;
            Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>> entries;
            if (stored.FormatVersion != formatVersion)
            {
                if (!IsDefaultState(stored))
                    Log.Default.Write($"{logName}のスキャン結果の保存形式が異なるため、すべて再走査します。");
                entries = new(StringComparer.OrdinalIgnoreCase);
                isDirty = true;
                discardedAllEntries = HasPersistedEntries(stored);
            }
            else if (!string.Equals(
                NormalizeEnvironmentFingerprint(stored.EnvironmentFingerprint),
                environmentFingerprint,
                StringComparison.Ordinal))
            {
                Log.Default.Write($"{logName}のスキャン環境が変わったため、すべて再走査します。");
                entries = new(StringComparer.OrdinalIgnoreCase);
                isDirty = true;
                discardedAllEntries = HasPersistedEntries(stored);
            }
            else
            {
                entries = NormalizeEntries(stored.Entries);
                isDirty = stored.Entries is null
                    || entries.Count != stored.Entries.Count
                    || !string.Equals(stored.EnvironmentFingerprint, environmentFingerprint, StringComparison.Ordinal);
            }
            foreach (var path in entries.Keys
                .Where(path => !currentPaths.Contains(path)
                    && (!configuredRoots.Any(root => IsPathWithinRoot(path, root))
                        || existingRoots.Any(root => IsPathWithinRoot(path, root)))
                    // 列挙を読み飛ばした可能性のあるルート配下は、消えたと断定できないため温存する
                    && !errorRoots.Any(root => IsPathWithinRoot(path, root)))
                .ToArray())
            {
                entries.Remove(path);
                isDirty = true;
            }

            getSignaturePath ??= static path => path;
            var signatureCache = new Dictionary<string, PluginModuleSignature?>(StringComparer.OrdinalIgnoreCase);
            var signatures = normalizedPaths.ToDictionary(
                path => path,
                path => TryCreateSignatureTarget(
                    path,
                    getSignaturePath,
                    logName,
                    signatureCache,
                    includeAdjacentDllsInSignature),
                StringComparer.OrdinalIgnoreCase);

            var plugins = new List<TPlugin>();
            var pathsToScan = new List<string>();
            if (refresh)
            {
                pathsToScan.AddRange(normalizedPaths);
            }
            else
            {
                foreach (var path in normalizedPaths)
                {
                    var signature = signatures[path];
                    if (signature is not null
                        && entries.TryGetValue(path, out var entry)
                        && entry.Signature == signature)
                    {
                        if (entry.Failed)
                            continue;
                        if (entry.Plugins is null || entry.Plugins.Any(x => !isValidPlugin(x)))
                        {
                            pathsToScan.Add(path);
                            continue;
                        }
                        plugins.AddRange(entry.Plugins.Select(x => setModulePath(x, path)));
                    }
                    else
                    {
                        pathsToScan.Add(path);
                    }
                }
            }

            if (pathsToScan.Count == 0)
            {
                // 全破棄直後にここへ来るのは列挙0件（未接続ルート等）の場合だけ。
                // 温存すべきエントリを空のまま確定させないため、破棄はこの経路では保存しない
                // （その間ファイルは旧エントリ・旧フィンガープリントのまま意図的に温存され、モジュールが再び現れた回に更新される）
                SaveIfDirty(storage, formatVersion, environmentFingerprint, entries, isDirty && !discardedAllEntries, logName);
                return new PersistentPluginScanResult<TPlugin>(plugins, !hasEnumerationErrors);
            }

            var scanResult = scan(pathsToScan);
            if (scanResult is null)
            {
                // 全破棄が未確定のままスキャンに失敗した場合は保存しない（旧キャッシュを温存し次回再試行）。
                // 全破棄時の entries は空なのでプルーニング由来のdirtyは存在しない
                SaveIfDirty(storage, formatVersion, environmentFingerprint, entries, isDirty && !discardedAllEntries, logName);
                return new PersistentPluginScanResult<TPlugin>(plugins, false);
            }

            var completed = scanResult.CompletedModulePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var skipped = scanResult.SkippedModulePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var timeoutSkipped = scanResult.TimeoutSkippedModulePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validScannedPlugins = scanResult.Plugins.Where(isValidPlugin).ToArray();
            // 全破棄の確定判定に使う「今回エントリへ実際に書き込めた完了モジュール数」（スキャナー申告のパス集合とのズレに備える）
            var persistedCompletedCount = 0;
            foreach (var path in pathsToScan)
            {
                var signature = signatures[path];
                if (signature is null)
                {
                    if (completed.Contains(path) && entries.Remove(path))
                        isDirty = true;
                    continue;
                }
                if (completed.Contains(path))
                {
                    persistedCompletedCount++;
                    var updatedEntry = new PersistentPluginScanCacheEntry<TPlugin>
                    {
                        Signature = signature,
                        Failed = false,
                        Plugins = validScannedPlugins
                            .Where(x => string.Equals(getModulePath(x), path, StringComparison.OrdinalIgnoreCase))
                            .Select(x => setModulePath(x, path))
                            .ToList(),
                    };
                    if (!entries.TryGetValue(path, out var existingEntry) || !AreEntriesEqual(existingEntry, updatedEntry, isValidPlugin, arePluginsEqual))
                    {
                        entries[path] = updatedEntry;
                        isDirty = true;
                    }
                }
                else if (skipped.Contains(path) && !timeoutSkipped.Contains(path))
                {
                    var updatedEntry = new PersistentPluginScanCacheEntry<TPlugin>
                    {
                        Signature = signature,
                        Failed = true,
                        Plugins = [],
                    };
                    if (!entries.TryGetValue(path, out var existingEntry) || !AreEntriesEqual(existingEntry, updatedEntry, isValidPlugin, arePluginsEqual))
                    {
                        entries[path] = updatedEntry;
                        isDirty = true;
                    }
                }
                // タイムアウト・未走査（スキャナー自体の失敗等）のモジュールは既存エントリを変更せず、
                // 署名一致の有効な旧結果があれば結果一覧へ復元する（refresh中の部分失敗で一覧から消さない）
                else if (entries.TryGetValue(path, out var existingEntry)
                    && existingEntry.Signature == signature
                    && !existingEntry.Failed
                    && existingEntry.Plugins is not null
                    && existingEntry.Plugins.All(isValidPlugin))
                {
                    plugins.AddRange(existingEntry.Plugins.Select(x => setModulePath(x, path)));
                }
            }

            plugins.AddRange(validScannedPlugins
                .Where(x => completed.Contains(getModulePath(x)))
                .Select(x => setModulePath(x, getModulePath(x))));
            var isComplete = !hasEnumerationErrors && pathsToScan.All(path => completed.Contains(path) || skipped.Contains(path));
            // 全破棄を含む保存は、1件以上エントリへ書き込めた完了か「タイムアウトが1件も無い走査の完遂」を確認できた場合のみ確定させる
            // （全モジュールがタイムアウトした回で旧キャッシュを空へ確定させない。タイムアウト非永続化の方針と整合）
            var allowSave = !discardedAllEntries || persistedCompletedCount > 0 || (isComplete && timeoutSkipped.Count == 0);
            SaveIfDirty(storage, formatVersion, environmentFingerprint, entries, isDirty && allowSave, logName);
            return new PersistentPluginScanResult<TPlugin>(plugins, isComplete);
        }

        static bool AreEntriesEqual<TPlugin>(
            PersistentPluginScanCacheEntry<TPlugin> left,
            PersistentPluginScanCacheEntry<TPlugin> right,
            Func<TPlugin, bool> isValidPlugin,
            Func<TPlugin, TPlugin, bool> arePluginsEqual)
        {
            if (left.Signature != right.Signature
                || left.Failed != right.Failed
                || left.Plugins is null
                || right.Plugins is null
                || left.Plugins.Count != right.Plugins.Count
                || left.Plugins.Any(x => !isValidPlugin(x))
                || right.Plugins.Any(x => !isValidPlugin(x)))
                return false;

            // スキャナーの出力順はプラグイン実装に依存するため、同値比較は順序に依存させない。
            var matched = new bool[right.Plugins.Count];
            foreach (var leftPlugin in left.Plugins)
            {
                var matchIndex = -1;
                for (var i = 0; i < right.Plugins.Count; i++)
                {
                    if (!matched[i] && arePluginsEqual(leftPlugin, right.Plugins[i]))
                    {
                        matchIndex = i;
                        break;
                    }
                }
                if (matchIndex < 0)
                    return false;
                matched[matchIndex] = true;
            }
            return true;
        }

        static void SaveIfDirty<TPlugin>(
            IPersistentPluginScanCacheStorage<TPlugin> storage,
            int formatVersion,
            string? environmentFingerprint,
            Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>> entries,
            bool isDirty,
            string logName)
        {
            if (!isDirty)
                return;
            try
            {
                storage.Save(new PersistentPluginScanCacheState<TPlugin>
                {
                    FormatVersion = formatVersion,
                    EnvironmentFingerprint = environmentFingerprint,
                    Entries = entries,
                });
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Default.Write($"{logName}のスキャン結果を保存できませんでした。", e);
            }
        }

        static PersistentPluginScanCacheState<TPlugin> LoadOrCreateEmpty<TPlugin>(
            IPersistentPluginScanCacheStorage<TPlugin> storage,
            string logName)
        {
            try
            {
                return storage.Load() ?? new PersistentPluginScanCacheState<TPlugin>();
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Default.Write($"{logName}の保存済みスキャン結果を読み込めないため、空の状態から再走査します。", e);
                return new PersistentPluginScanCacheState<TPlugin>();
            }
        }

        static bool IsDefaultState<TPlugin>(PersistentPluginScanCacheState<TPlugin> state)
            => state.FormatVersion == 0
                && string.IsNullOrEmpty(state.EnvironmentFingerprint)
                && (state.Entries is null || state.Entries.Count == 0);

        /// <summary>破棄で実際に失う（正規化後も残る）エントリがあるかどうか</summary>
        static bool HasPersistedEntries<TPlugin>(PersistentPluginScanCacheState<TPlugin> state)
            => state.Entries is not null
                && state.Entries.Any(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value is not null);

        internal static PersistentPluginScanCacheState<TPlugin> CloneState<TPlugin>(PersistentPluginScanCacheState<TPlugin> state)
            => new()
            {
                FormatVersion = state.FormatVersion,
                EnvironmentFingerprint = state.EnvironmentFingerprint,
                Entries = CloneEntries(state.Entries),
            };

        internal static Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>> CloneEntries<TPlugin>(
            Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>>? entries)
            => NormalizeEntries(entries).ToDictionary(
                x => x.Key,
                x => new PersistentPluginScanCacheEntry<TPlugin>
                {
                    Signature = x.Value.Signature,
                    Failed = x.Value.Failed,
                    Plugins = x.Value.Plugins is null ? [] : [.. x.Value.Plugins],
                },
                StringComparer.OrdinalIgnoreCase);

        static Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>> NormalizeEntries<TPlugin>(
            Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>>? entries)
            => (entries ?? new Dictionary<string, PersistentPluginScanCacheEntry<TPlugin>>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        static PluginModuleSignature? TryCreateSignatureTarget(
            string modulePath,
            Func<string, string> getSignaturePath,
            string logName,
            Dictionary<string, PluginModuleSignature?> signatureCache,
            Func<string, bool>? includeAdjacentDllsInSignature)
        {
            try
            {
                var signaturePath = Path.GetFullPath(getSignaturePath(modulePath));
                var includeAdjacentDlls = includeAdjacentDllsInSignature?.Invoke(modulePath) == true;
                var signatureCacheKey = includeAdjacentDlls ? signaturePath + "\0dll" : signaturePath;
                if (!signatureCache.TryGetValue(signatureCacheKey, out var signature))
                {
                    signature = TryCreateSignature(signaturePath, logName, includeAdjacentDlls);
                    signatureCache.Add(signatureCacheKey, signature);
                }
                return signature;
            }
            catch (Exception e)
            {
                Log.Default.Write($"{logName}の署名対象パスを解決できないため再走査します。path={modulePath}", e);
                return null;
            }
        }

        internal static PluginModuleSignature? TryCreateSignature(string path, string logName)
            => TryCreateSignature(path, logName, includeAdjacentDlls: false);

        internal static PluginModuleSignature? TryCreateSignature(string path, string logName, bool includeAdjacentDlls)
        {
            try
            {
                if (File.Exists(path))
                {
                    var file = new FileInfo(path);
                    if (includeAdjacentDlls)
                    {
                        // "*.dll" は3文字拡張子の仕様で "dllx" 等も一致するため、拡張子の完全一致で絞り直す
                        var adjacentFiles = new[] { file }
                            .Concat((file.Directory?.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly) ?? [])
                                .Where(x => x.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                            .Select(x => new
                            {
                                RelativePath = x.Name.ToLowerInvariant(),
                                x.Length,
                                LastWriteTimeUtcTicks = x.LastWriteTimeUtc.Ticks,
                            })
                            .OrderBy(x => x.RelativePath, StringComparer.Ordinal)
                            .ToArray();
                        return CreateFileSetSignature(adjacentFiles.Select(x => (x.RelativePath, x.Length, x.LastWriteTimeUtcTicks)));
                    }
                    return new PluginModuleSignature(1, file.Length, file.LastWriteTimeUtc.Ticks);
                }
                if (!Directory.Exists(path))
                    return null;

                var files = EnumerateDirectoryFiles(path)
                    .Select(file =>
                    {
                        return new
                        {
                            RelativePath = Path.GetRelativePath(path, file.FullName)
                                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
                            file.Length,
                            LastWriteTimeUtcTicks = file.LastWriteTimeUtc.Ticks,
                        };
                    })
                    .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.RelativePath, StringComparer.Ordinal)
                    .ToArray();
                return CreateFileSetSignature(files.Select(x => (x.RelativePath, x.Length, x.LastWriteTimeUtcTicks)));
            }
            catch (Exception e)
            {
                Log.Default.Write($"{logName}の変更確認に失敗したため再走査します。path={path}", e);
                return null;
            }
        }

        static PluginModuleSignature CreateFileSetSignature(IEnumerable<(string RelativePath, long Length, long LastWriteTimeUtcTicks)> fileSet)
        {
            var files = fileSet.ToArray();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files)
            {
                AppendHashField(hash, file.RelativePath);
                AppendHashField(hash, file.Length.ToString(CultureInfo.InvariantCulture));
                AppendHashField(hash, file.LastWriteTimeUtcTicks.ToString(CultureInfo.InvariantCulture));
            }
            return new PluginModuleSignature(
                files.LongLength,
                files.Sum(x => x.Length),
                files.Length == 0 ? 0 : files.Max(x => x.LastWriteTimeUtcTicks),
                Convert.ToHexString(hash.GetHashAndReset()));
        }

        static IEnumerable<FileInfo> EnumerateDirectoryFiles(string root)
        {
            var directories = new Stack<DirectoryInfo>();
            directories.Push(new DirectoryInfo(root));
            while (directories.Count > 0)
            {
                FileSystemInfo[] entries;
                try
                {
                    entries = [.. directories.Pop().EnumerateFileSystemInfos()];
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                foreach (var entry in entries)
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = entry.Attributes;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;
                    if (entry is DirectoryInfo directory)
                        directories.Push(directory);
                    else if (entry is FileInfo file)
                        yield return file;
                }
            }
        }

        static void AppendHashField(IncrementalHash hash, string value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
            hash.AppendData([0]);
        }

        internal static List<string> NormalizePaths(IEnumerable<string> paths, string logName)
        {
            var results = new List<string>();
            foreach (var path in paths.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    results.Add(Path.GetFullPath(path));
                }
                catch (Exception e)
                {
                    Log.Default.Write($"{logName}のパスを解決できないため走査対象から除外します。path={path}", e);
                }
            }
            return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static bool IsPathWithinRoot(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return true;
            var rootWithSeparator = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
            return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        internal static PluginModuleEnumerationErrorKind ClassifyEnumerationException(Exception exception)
            => exception switch
            {
                FileNotFoundException or DirectoryNotFoundException => PluginModuleEnumerationErrorKind.None,
                UnauthorizedAccessException => PluginModuleEnumerationErrorKind.Permanent,
                IOException => PluginModuleEnumerationErrorKind.Transient,
                // catch節から呼ばれるため未知の例外でもthrowしない。安全側（プルーニング抑止＋再試行）へ倒す
                _ => PluginModuleEnumerationErrorKind.Transient,
            };

        internal static string GetScannerFileVersion(string? scannerPath)
        {
            if (string.IsNullOrEmpty(scannerPath) || !File.Exists(scannerPath))
                return "0.0.0.0";
            try
            {
                return FileVersionInfo.GetVersionInfo(scannerPath).FileVersion is { Length: > 0 } version
                    ? version
                    : "0.0.0.0";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return "0.0.0.0";
            }
        }

        static string? NormalizeEnvironmentFingerprint(string? environmentFingerprint)
            => string.IsNullOrEmpty(environmentFingerprint) ? null : environmentFingerprint;
    }
}
