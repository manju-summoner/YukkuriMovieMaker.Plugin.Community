using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>YMM4で使用できないOFXプラグインの非対応理由</summary>
    internal enum OpenFxUnsupportedReason
    {
        /// <summary>対応コンテキスト（フィルター・トランジション・ジェネレーター）を1つも宣言していない</summary>
        NoSupportedContext,
        /// <summary>32bit float画像に非対応</summary>
        FloatDepth,
        /// <summary>単一インスタンス制約あり</summary>
        SingleInstance,
        /// <summary>前後フレームの取得（テンポラルアクセス）が必要</summary>
        TemporalClipAccess,
        /// <summary>CPUレンダリング非対応で、利用可能なCUDAバックエンドにも対応していない</summary>
        GpuOnly,
    }

    /// <summary>describeで得たGPU/CPUレンダリング宣言（ofxGPURender.hの生文字列）</summary>
    internal record OpenFxGpuSupport(
        string OpenGL,
        string Cuda,
        string CudaStream,
        string OpenCLRender,
        string OpenCL,
        string Metal,
        string CPU)
    {
        public static OpenFxGpuSupport Default { get; } = new("false", "false", "false", "false", "false", "false", "true");

        static bool IsEnabled(string value)
            // 一部の既存プラグインがCUDA/OpenCLにもOpenGL由来の"needed"を流用するため、
            // 規格値ではない系統でも互換性優先で有効宣言として扱う。
            => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("needed", StringComparison.OrdinalIgnoreCase);

        public bool SupportsOpenGL => IsEnabled(OpenGL);
        public bool SupportsCuda => IsEnabled(Cuda);
        public bool SupportsOpenCL => IsEnabled(OpenCLRender) || IsEnabled(OpenCL);
        public bool SupportsCPU => !CPU.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// スキャンで見つかったOFXプラグイン1つ分の情報。
    /// SupportsFilter / SupportsTransition / SupportsGenerator は対応コンテキストの宣言で、
    /// 映像エフェクトの一覧はフィルター対応のみ・場面切り替えの一覧はトランジション対応のみ・
    /// 図形の一覧はジェネレーター対応のみを表示する。
    /// UnsupportedReasonが非nullのプラグインはYMM4では使用できず、Supports*はすべてfalseになる
    /// （設定画面の一覧にだけ「非対応」として表示し、各エフェクトの選択肢には現れない）
    /// </summary>
    internal record OpenFxPluginInfo(string BinaryPath, string Identifier, uint VersionMajor, uint VersionMinor, string Name, string Grouping, bool SupportsFilter, bool SupportsTransition, bool SupportsGenerator, OpenFxUnsupportedReason? UnsupportedReason = null, OpenFxGpuSupport? DeclaredGpuSupport = null)
    {
        internal bool DeclaredSupportsFilter { get; init; } = SupportsFilter;
        internal bool DeclaredSupportsTransition { get; init; } = SupportsTransition;
        internal bool DeclaredSupportsGenerator { get; init; } = SupportsGenerator;
        public OpenFxGpuSupport GpuSupport => DeclaredGpuSupport ?? OpenFxGpuSupport.Default;
        public string DisplayName => string.IsNullOrEmpty(Grouping) ? Name : $"{Name} ({Grouping})";

        /// <summary>
        /// 対応コンテキストの表示文字列（設定画面のプラグイン一覧用）。
        /// トランジション専用プラグインが映像エフェクト側で選べない理由や、
        /// 認識はしているが使用できないプラグインをユーザーが判別できるようにする
        /// </summary>
        public string SupportedUsagesText => UnsupportedReason is null
            ? string.Join(" / ", EnumerateUsageLabels())
            : Texts.OpenFxSettingsPluginUsageUnsupported;

        /// <summary>非対応プラグインかどうか（設定画面のツールチップ有効化バインディング用）</summary>
        public bool IsUnsupported => UnsupportedReason is not null;

        /// <summary>非対応理由の説明（設定画面のツールチップ用）。対応プラグインではnull</summary>
        public string? UnsupportedReasonText => UnsupportedReason switch
        {
            OpenFxUnsupportedReason.NoSupportedContext => Texts.OpenFxSettingsUnsupportedNoContextToolTip,
            OpenFxUnsupportedReason.FloatDepth => Texts.OpenFxSettingsUnsupportedFloatDepthToolTip,
            OpenFxUnsupportedReason.SingleInstance => Texts.OpenFxSettingsUnsupportedSingleInstanceToolTip,
            OpenFxUnsupportedReason.TemporalClipAccess => Texts.OpenFxSettingsUnsupportedTemporalToolTip,
            OpenFxUnsupportedReason.GpuOnly => Texts.OpenFxSettingsUnsupportedGpuOnlyToolTip,
            _ => null,
        };

        /// <summary>設定画面へ表示するGPUレンダリング宣言</summary>
        public string GpuSupportText
        {
            get
            {
                var labels = new List<string>(3);
                if (GpuSupport.SupportsOpenGL)
                    labels.Add("OpenGL");
                if (GpuSupport.SupportsCuda)
                    labels.Add("CUDA");
                if (GpuSupport.SupportsOpenCL)
                    labels.Add("OpenCL");
                return labels.Count > 0 ? string.Join(" / ", labels) : "-";
            }
        }

        IEnumerable<string> EnumerateUsageLabels()
        {
            if (SupportsFilter)
                yield return Texts.OpenFxSettingsPluginUsageFilter;
            if (SupportsTransition)
                yield return Texts.OpenFxSettingsPluginUsageTransition;
            if (SupportsGenerator)
                yield return Texts.OpenFxSettingsPluginUsageShape;
        }
    }

    /// <summary>
    /// OFXプラグイン（.ofxバンドル）をシステムのOFXディレクトリから列挙する。
    /// 全バイナリをロードしてdescribeまで行うため初回は時間がかかる。結果はセッション内でキャッシュされる。
    /// </summary>
    internal static class OpenFxPluginScanner
    {
        static readonly object lockObject = new();
        static volatile IReadOnlyList<OpenFxPluginInfo>? cache;
        static bool cachedUseGpuRendering;

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
                var useGpuRendering = OpenFxSettings.Default.UseGpuRendering;
                if (cache is not null && !refresh)
                {
                    // CUDA可否はプロセス中不変のLazy値なので、設定だけをキャッシュ済みdescribe結果から再評価する。
                    if (cachedUseGpuRendering != useGpuRendering)
                    {
                        ReevaluateGpuOnlySupport(useGpuRendering);
                        cachedUseGpuRendering = useGpuRendering;
                    }
                    return cache;
                }

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
                cachedUseGpuRendering = useGpuRendering;
                return cache;
            }
        }

        /// <summary>キャッシュ済みdescribe結果からGPU専用プラグインの対応可否だけを再評価する。</summary>
        public static IReadOnlyList<OpenFxPluginInfo>? ReevaluateCachedPlugins()
        {
            lock (lockObject)
            {
                if (cache is null)
                    return null;
                var useGpuRendering = OpenFxSettings.Default.UseGpuRendering;
                ReevaluateGpuOnlySupport(useGpuRendering);
                cachedUseGpuRendering = useGpuRendering;
                return cache;
            }
        }

        static void ReevaluateGpuOnlySupport(bool useGpuRendering)
        {
            cache = cache!
                .Select(plugin =>
                {
                    if (plugin.UnsupportedReason is not null and not OpenFxUnsupportedReason.GpuOnly)
                        return plugin;
                    var gpuOnlyUnsupported = !plugin.GpuSupport.SupportsCPU
                        && !(useGpuRendering
                            && plugin.GpuSupport.SupportsCuda
                            && OfxGpuRenderBackendFactory.HasRegisteredBackend);
                    return plugin with
                    {
                        SupportsFilter = !gpuOnlyUnsupported && plugin.DeclaredSupportsFilter,
                        SupportsTransition = !gpuOnlyUnsupported && plugin.DeclaredSupportsTransition,
                        SupportsGenerator = !gpuOnlyUnsupported && plugin.DeclaredSupportsGenerator,
                        UnsupportedReason = gpuOnlyUnsupported ? OpenFxUnsupportedReason.GpuOnly : null,
                    };
                })
                .ToArray();
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
                    plugins.Add(CreatePluginInfo(
                        binaryPath,
                        plugin.Identifier,
                        plugin.VersionMajor,
                        plugin.VersionMinor,
                        descriptor.Label,
                        descriptor.Grouping,
                        descriptor.SupportedContexts,
                        descriptor.SupportedPixelDepths,
                        descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPluginPropSingleInstance, 0) != 0,
                        descriptor.Props.GetIntOrDefault(OfxConstants.ImageEffectPropTemporalClipAccess, 0) != 0,
                        GetGpuSupport(descriptor.Props)));
                }
                catch (Exception e)
                {
                    reportError($"plugin={plugin.Identifier} {e.Message}");
                }
            }
            return plugins;
        }

        /// <summary>
        /// describe結果から対応可否を判定してプラグイン情報を作る。
        /// 対応外のプラグインも設定画面の一覧へ「非対応」として表示するため、除外せずUnsupportedReasonを設定して返す。
        /// 隔離スキャン（ネイティブスキャナーが返す生のdescribe結果）とプロセス内スキャンの共通判定
        /// </summary>
        internal static OpenFxPluginInfo CreatePluginInfo(
            string binaryPath,
            string identifier,
            uint versionMajor,
            uint versionMinor,
            string label,
            string grouping,
            IReadOnlyCollection<string> supportedContexts,
            IReadOnlyCollection<string> supportedPixelDepths,
            bool isSingleInstance,
            bool needsTemporalClipAccess,
            OpenFxGpuSupport? gpuSupport = null,
            bool? useGpuRendering = null)
        {
            gpuSupport ??= OpenFxGpuSupport.Default;
            useGpuRendering ??= OpenFxSettings.Default.UseGpuRendering;
            // 対応済みのコンテキストはフィルター＝映像エフェクト、トランジション＝場面切り替え、ジェネレーター＝図形
            var supportsFilter = supportedContexts.Contains(OfxConstants.ImageEffectContextFilter);
            var supportsTransition = supportedContexts.Contains(OfxConstants.ImageEffectContextTransition);
            var supportsGenerator = supportedContexts.Contains(OfxConstants.ImageEffectContextGenerator);
            // 非対応の判定基準：
            // - 単一インスタンス制約：エフェクト項目ごとにインスタンスを生成するため対応できない
            // - テンポラルアクセス（前後フレームの取得）：未対応。対応と偽って現在フレームを返すと黙って誤った出力になる
            var unsupportedReason =
                !supportsFilter && !supportsTransition && !supportsGenerator ? OpenFxUnsupportedReason.NoSupportedContext
                : !supportedPixelDepths.Contains(OfxConstants.BitDepthFloat) ? OpenFxUnsupportedReason.FloatDepth
                : isSingleInstance ? OpenFxUnsupportedReason.SingleInstance
                : needsTemporalClipAccess ? OpenFxUnsupportedReason.TemporalClipAccess
                : !gpuSupport.SupportsCPU
                    && !(useGpuRendering.Value
                        && gpuSupport.SupportsCuda
                        && OfxGpuRenderBackendFactory.HasRegisteredBackend)
                    ? OpenFxUnsupportedReason.GpuOnly
                : (OpenFxUnsupportedReason?)null;
            if (unsupportedReason is not null)
            {
                // 非対応プラグインが各エフェクトの選択肢に現れないよう、対応フラグはすべて落とす
                supportsFilter = supportsTransition = supportsGenerator = false;
            }
            var name = label is { Length: > 0 } ? label : identifier.Split('.').Last();
            return new OpenFxPluginInfo(binaryPath, identifier, versionMajor, versionMinor, name, grouping, supportsFilter, supportsTransition, supportsGenerator, unsupportedReason, gpuSupport)
            {
                DeclaredSupportsFilter = supportedContexts.Contains(OfxConstants.ImageEffectContextFilter),
                DeclaredSupportsTransition = supportedContexts.Contains(OfxConstants.ImageEffectContextTransition),
                DeclaredSupportsGenerator = supportedContexts.Contains(OfxConstants.ImageEffectContextGenerator),
            };
        }

        static OpenFxGpuSupport GetGpuSupport(OfxPropertySet props)
            => new(
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenGLRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCudaRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCudaStreamSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenCLRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenCLSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropMetalRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCPURenderSupported, "true"));

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
