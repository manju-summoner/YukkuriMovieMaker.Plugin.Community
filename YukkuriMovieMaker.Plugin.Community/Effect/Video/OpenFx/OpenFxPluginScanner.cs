using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// スキャンで見つかったOFXプラグイン1つ分の情報。
    /// SupportsFilter / SupportsTransition は対応コンテキストの宣言で、
    /// 映像エフェクトの一覧はフィルター対応のみ・場面切替えの一覧はトランジション対応のみを表示する
    /// </summary>
    internal record OpenFxPluginInfo(string BinaryPath, string Identifier, uint VersionMajor, uint VersionMinor, string Name, string Grouping, bool SupportsFilter, bool SupportsTransition)
    {
        public string DisplayName => string.IsNullOrEmpty(Grouping) ? Name : $"{Name} ({Grouping})";
    }

    /// <summary>
    /// OFXプラグイン（.ofxバンドル）をシステムのOFXディレクトリから列挙する。
    /// 全バイナリをロードしてdescribeまで行うため初回は時間がかかる。結果はセッション内でキャッシュされる。
    /// </summary>
    internal static class OpenFxPluginScanner
    {
        static readonly object lockObject = new();
        static volatile IReadOnlyList<OpenFxPluginInfo>? cache;

        /// <summary>
        /// スキャン済みの結果。未スキャンならnull。
        /// スキャン実行中のUIスレッドからも呼ばれるため、スキャンを囲むロックは取らない
        /// </summary>
        public static IReadOnlyList<OpenFxPluginInfo>? CachedPlugins => cache;

        /// <summary>
        /// 標準のOFX検索フォルダー（存在しないものも含む）。
        /// IsUserManagedはYMM4のプラグインフォルダー配下（YMM4が作成してよいフォルダー）かどうか
        /// </summary>
        public static IEnumerable<(string Path, bool IsUserManaged)> GetDefaultDirectoryInfos()
        {
            // OFX規格の標準インストール場所（<CommonProgramFiles>\OFX\Plugins）
            yield return (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "OFX", "Plugins"), false);
            // user\plugin はYMM4用プラグインのフォルダーのため、OFXプラグインは user\resources 配下に置く
            yield return (Path.Combine(AppDirectories.UserResourceDirectory, "ofx"), true);
        }

        public static IEnumerable<string> GetDefaultDirectories()
            => GetDefaultDirectoryInfos().Select(x => x.Path);

        public static IReadOnlyList<OpenFxPluginInfo> GetEffectPlugins(bool refresh = false)
        {
            lock (lockObject)
            {
                if (cache is not null && !refresh)
                    return cache;

                // ルート同士が入れ子（追加フォルダーに既定フォルダーの配下を指定等）でも同じバイナリを二重スキャンしない
                // （表記ゆれで重複が残らないようフルパスへ正規化してから比較する）
                var binaryPaths = EnumerateBinaryPaths()
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var plugins = ScanIsolated(binaryPaths) ?? ScanInProcess(binaryPaths);
                cache = plugins
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return cache;
            }
        }

        /// <summary>
        /// スキャナープロセスによる隔離スキャン。壊れたプラグインがあってもYMM4本体は巻き込まれない。
        /// スキャナーEXEが見つからない・起動できない場合はnull
        /// </summary>
        static List<OpenFxPluginInfo>? ScanIsolated(List<string> binaryPaths)
        {
            var scannerPath = OpenFxScannerProcess.FindScannerPath();
            if (scannerPath is null)
            {
                Log.Default.Write($"{OpenFxScannerProcess.ExeName}が見つからないため、プロセス内でOFXをスキャンします。");
                return null;
            }
            try
            {
                return OpenFxScannerProcess.Scan(scannerPath, binaryPaths);
            }
            catch (Exception e)
            {
                Log.Default.Write("OFXスキャナープロセスの実行に失敗したため、プロセス内でOFXをスキャンします。", e);
                return null;
            }
        }

        /// <summary>
        /// 指定したバイナリから指定IDのプラグイン（最新バージョン）を取得する
        /// </summary>
        public static OfxImageEffectPlugin? LoadPlugin(string binaryPath, string identifier)
        {
            var binary = OfxPluginBinary.Load(binaryPath);
            return binary.EnumerateImageEffectPlugins()
                .Where(p => string.Equals(p.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.VersionMajor)
                .ThenByDescending(p => p.VersionMinor)
                .FirstOrDefault();
        }

        /// <summary>
        /// プロセス内スキャン（スキャナーEXEが使えない環境向けのフォールバック）。
        /// バイナリのロードとdescribeを本体プロセスで行うため、壊れたプラグインのクラッシュには巻き込まれる
        /// </summary>
        static List<OpenFxPluginInfo> ScanInProcess(List<string> binaryPaths)
        {
            var plugins = new List<OpenFxPluginInfo>();
            foreach (var binaryPath in binaryPaths)
            {
                try
                {
                    plugins.AddRange(ScanBinary(
                        binaryPath,
                        error => Log.Default.Write($"OFXプラグインのdescribeに失敗しました。path={binaryPath} error={error}")));
                }
                catch (Exception e)
                {
                    // 壊れたバイナリや非対応アーキテクチャはスキップする
                    Log.Default.Write($"OFXバイナリの走査に失敗しました。path={binaryPath}", e);
                }
            }
            return plugins;
        }

        /// <summary>
        /// バイナリ1つ分をスキャンして対応プラグインを列挙する。
        /// describeに失敗したプラグインは reportError へ通知してスキップする（バイナリのロード失敗は例外）
        /// </summary>
        internal static IEnumerable<OpenFxPluginInfo> ScanBinary(string binaryPath, Action<string> reportError)
        {
            var plugins = new List<OpenFxPluginInfo>();
            var binary = OfxPluginBinary.Load(binaryPath);
            // 同一IDの複数バージョン登録（後方互換用）は最新バージョンだけを一覧に載せる
            foreach (var group in binary.EnumerateImageEffectPlugins().GroupBy(p => p.Identifier, StringComparer.OrdinalIgnoreCase))
            {
                var plugin = group
                    .OrderByDescending(p => p.VersionMajor)
                    .ThenByDescending(p => p.VersionMinor)
                    .First();
                try
                {
                    var descriptor = plugin.Describe();
                    var info = TryCreatePluginInfo(
                        binaryPath,
                        plugin.Identifier,
                        plugin.VersionMajor,
                        plugin.VersionMinor,
                        descriptor.Label,
                        descriptor.Grouping,
                        descriptor.SupportedContexts,
                        descriptor.SupportedPixelDepths,
                        descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPluginPropSingleInstance, 0) != 0,
                        descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPropTemporalClipAccess, 0) != 0);
                    if (info is not null)
                        plugins.Add(info);
                }
                catch (Exception e)
                {
                    reportError($"plugin={plugin.Identifier} {e.Message}");
                }
            }
            return plugins;
        }

        /// <summary>
        /// describe結果から対応可否を判定してプラグイン情報を作る（対応外ならnull）。
        /// 隔離スキャン（ネイティブスキャナーが返す生のdescribe結果）とプロセス内スキャンの共通判定
        /// </summary>
        internal static OpenFxPluginInfo? TryCreatePluginInfo(
            string binaryPath,
            string identifier,
            uint versionMajor,
            uint versionMinor,
            string label,
            string grouping,
            IReadOnlyCollection<string> supportedContexts,
            IReadOnlyCollection<string> supportedPixelDepths,
            bool isSingleInstance,
            bool needsTemporalClipAccess)
        {
            // 対応済みのコンテキスト（フィルター＝映像エフェクト、トランジション＝場面切替え）を
            // 1つも宣言しないプラグインは一覧に載せない
            var supportsFilter = supportedContexts.Contains(OfxConstants.ImageEffectContextFilter);
            var supportsTransition = supportedContexts.Contains(OfxConstants.ImageEffectContextTransition);
            if (!supportsFilter && !supportsTransition)
                return null;
            if (!supportedPixelDepths.Contains(OfxConstants.BitDepthFloat))
                return null;
            // エフェクト項目ごとにインスタンスを生成するため、単一インスタンス制約のプラグインは対応外
            if (isSingleInstance)
                return null;
            // テンポラルアクセス（前後フレームの取得）は未対応。
            // 対応と偽って現在フレームを返すと黙って誤った出力になるため一覧から除外する
            if (needsTemporalClipAccess)
                return null;
            var name = label is { Length: > 0 } ? label : identifier.Split('.').Last();
            return new OpenFxPluginInfo(binaryPath, identifier, versionMajor, versionMinor, name, grouping, supportsFilter, supportsTransition);
        }

        static IEnumerable<string> EnumerateBinaryPaths()
        {
            var roots = GetDefaultDirectories()
                .Concat(OpenFxSettings.Default.AdditionalPluginDirectories)
                .Where(x => !string.IsNullOrWhiteSpace(x));
            foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // OFXプラグインはバンドル形式（<名前>.ofx.bundle\Contents\Win64\<名前>.ofx）が標準。
                // 直接置かれた .ofx 単体ファイルも受け付ける
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
                        continue;
                    }
                    foreach (var entry in entries)
                    {
                        if (Directory.Exists(entry))
                        {
                            if (entry.EndsWith(".ofx.bundle", StringComparison.OrdinalIgnoreCase))
                            {
                                // バンドルは固定の相対パスを読むだけで再帰しないため、
                                // バンドル自体がジャンクション等のリンクでも受け付けてよい
                                var win64 = Path.Combine(entry, "Contents", "Win64");
                                if (Directory.Exists(win64))
                                {
                                    string[] binaries;
                                    try
                                    {
                                        binaries = Directory.GetFiles(win64, "*.ofx");
                                    }
                                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                                    {
                                        continue;
                                    }
                                    foreach (var binary in binaries)
                                        yield return binary;
                                }
                            }
                            else
                            {
                                // ジャンクション・シンボリックリンクは辿らない（親を指すリンクによる無限ループ防止）
                                FileAttributes attributes;
                                try
                                {
                                    attributes = File.GetAttributes(entry);
                                }
                                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                                {
                                    continue;
                                }
                                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                                    continue;
                                directories.Push(entry);
                            }
                        }
                        else if (entry.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return entry;
                        }
                    }
                }
            }
        }
    }
}
