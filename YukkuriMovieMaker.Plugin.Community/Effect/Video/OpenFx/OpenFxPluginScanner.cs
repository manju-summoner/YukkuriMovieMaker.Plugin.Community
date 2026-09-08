using Newtonsoft.Json;
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

        [JsonIgnore]
        public bool SupportsOpenGL => IsEnabled(OpenGL);
        [JsonIgnore]
        public bool SupportsCuda => IsEnabled(Cuda);
        [JsonIgnore]
        public bool SupportsOpenCLBuffer => IsEnabled(OpenCLRender);
        [JsonIgnore]
        public bool SupportsOpenCL => IsEnabled(OpenCLRender) || IsEnabled(OpenCL);
        [JsonIgnore]
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
    /// 全バイナリをロードしてdescribeまで行うため初回は時間がかかる。生のdescribe結果はセッション内とディスクへキャッシュされる。
    /// </summary>
    internal static class OpenFxPluginScanner
    {
        static readonly object lockObject = new();
        static volatile IReadOnlyList<OpenFxPluginInfo>? cache;
        static IReadOnlyList<OpenFxPluginInfo>? incompleteCache;
        static bool cachedUseGpuRendering;
        static bool incompleteCacheUseGpuRendering;
        static long lastAutomaticScanAttemptTick = -1;
        const long AutomaticScanRetryIntervalMilliseconds = 30_000;
        // PLUGIN行のフィールド構成やOpenFxScannedPluginInfoのレイアウトを変えた場合は上げること。
        const int PersistentCacheFormatVersion = 1;
        internal static IPersistentPluginScanCacheStorage<OpenFxScannedPluginInfo> PersistentCacheStorage { get; set; } = new OpenFxScanCacheSettingsStorage();

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
            var scannerPath = new Lazy<string?>(OpenFxScannerProcess.FindScannerPath);
            return GetEffectPlugins(
                refresh,
                EnumerateBinaries,
                PersistentCacheStorage,
                binaryPaths => ScanIsolatedDetailed(scannerPath.Value, binaryPaths),
                OpenFxSettings.Default.UseGpuRendering,
                OfxGpuRenderBackendFactory.IsDeclaredBackendAvailable,
                () => OpenFxScannerProcess.GetEnvironmentFingerprint(scannerPath.Value));
        }

        internal static IReadOnlyList<OpenFxPluginInfo> GetEffectPlugins(
            bool refresh,
            Func<PluginModuleEnumerationResult> enumerateBinaries,
            IPersistentPluginScanCacheStorage<OpenFxScannedPluginInfo> persistentCacheStorage,
            Func<IReadOnlyList<string>, PluginModuleScanResult<OpenFxScannedPluginInfo>?> scan,
            bool useGpuRendering,
            Func<bool, bool, bool> isDeclaredBackendAvailable,
            Func<string?> getEnvironmentFingerprint)
        {
            lock (lockObject)
            {
                if (cache is not null && !refresh)
                {
                    if (cachedUseGpuRendering != useGpuRendering)
                    {
                        cache = ReevaluateGpuOnlySupport(cache, useGpuRendering, isDeclaredBackendAvailable);
                        cachedUseGpuRendering = useGpuRendering;
                    }
                    return cache;
                }
                var now = Environment.TickCount64;
                if (!refresh
                    && incompleteCache is not null
                    && lastAutomaticScanAttemptTick >= 0
                    && now - lastAutomaticScanAttemptTick < AutomaticScanRetryIntervalMilliseconds)
                {
                    if (incompleteCacheUseGpuRendering != useGpuRendering)
                    {
                        incompleteCache = ReevaluateGpuOnlySupport(incompleteCache, useGpuRendering, isDeclaredBackendAvailable);
                        incompleteCacheUseGpuRendering = useGpuRendering;
                    }
                    return incompleteCache;
                }

                var result = PersistentPluginScanCache.Scan(
                    refresh,
                    enumerateBinaries(),
                    persistentCacheStorage,
                    PersistentCacheFormatVersion,
                    "OFXバイナリ",
                    scan,
                    x => x.BinaryPath,
                    (x, path) => x with { BinaryPath = path },
                    IsValidScannedPlugin,
                    getSignaturePath: GetSignaturePath,
                    includeAdjacentDllsInSignature: IsStandaloneBinary,
                    getEnvironmentFingerprint: getEnvironmentFingerprint,
                    arePluginsEqual: AreScannedPluginsEqual);
                if (!result.IsComplete)
                {
                    // 明示的な再走査の後は、直前の自動失敗による抑止時刻を残さない（次の自動呼び出しを妨げない）
                    lastAutomaticScanAttemptTick = refresh ? -1L : Environment.TickCount64;
                    if (cache is not null)
                    {
                        // refresh失敗でここへ来た場合もGPU設定の変更を反映してから返す
                        if (cachedUseGpuRendering != useGpuRendering)
                        {
                            cache = ReevaluateGpuOnlySupport(cache, useGpuRendering, isDeclaredBackendAvailable);
                            cachedUseGpuRendering = useGpuRendering;
                        }
                        return cache;
                    }
                    incompleteCache = result.Plugins
                        .Select(x => CreatePluginInfo(x, useGpuRendering, isDeclaredBackendAvailable))
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    incompleteCacheUseGpuRendering = useGpuRendering;
                    return incompleteCache;
                }

                cache = result.Plugins
                    .Select(x => CreatePluginInfo(x, useGpuRendering, isDeclaredBackendAvailable))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                cachedUseGpuRendering = useGpuRendering;
                incompleteCache = null;
                return cache;
            }
        }

        static bool AreScannedPluginsEqual(OpenFxScannedPluginInfo left, OpenFxScannedPluginInfo right)
            => left.BinaryPath == right.BinaryPath
                && left.Identifier == right.Identifier
                && left.VersionMajor == right.VersionMajor
                && left.VersionMinor == right.VersionMinor
                && left.Label == right.Label
                && left.Grouping == right.Grouping
                && left.SupportedContexts.SequenceEqual(right.SupportedContexts, StringComparer.Ordinal)
                && left.SupportedPixelDepths.SequenceEqual(right.SupportedPixelDepths, StringComparer.Ordinal)
                && left.IsSingleInstance == right.IsSingleInstance
                && left.NeedsTemporalClipAccess == right.NeedsTemporalClipAccess
                && left.GpuSupport == right.GpuSupport;

        static bool IsValidScannedPlugin(OpenFxScannedPluginInfo? x)
            => x is not null
                && x.Identifier is not null
                && x.Label is not null
                && x.Grouping is not null
                && x.SupportedContexts is not null
                && x.SupportedPixelDepths is not null
                && x.GpuSupport is
                {
                    OpenGL: not null,
                    Cuda: not null,
                    CudaStream: not null,
                    OpenCLRender: not null,
                    OpenCL: not null,
                    Metal: not null,
                    CPU: not null,
                };

        /// <summary>
        /// スキャンせずに、現時点で分かっているプラグイン一覧を現在のGPU設定で評価して返す。
        /// このセッションでスキャンが完了していればその結果、そうでなければ前回までに保存した結果
        /// （フォルダー列挙もスキャナー起動も行わないため、プラグインの増減は更新ボタンによる再走査まで反映されない）。
        /// 失敗した再走査の部分結果は保存結果より情報が少ない（スキャナー起動失敗時は空になる）ため、
        /// 完了した結果が無い間は常に保存結果へ戻る。スキャン実行中に呼ばれた場合はその完了を待つ
        /// </summary>
        public static IReadOnlyList<OpenFxPluginInfo> GetKnownPlugins()
            => GetKnownPlugins(
                PersistentCacheStorage,
                OpenFxSettings.Default.UseGpuRendering,
                OfxGpuRenderBackendFactory.IsDeclaredBackendAvailable);

        internal static IReadOnlyList<OpenFxPluginInfo> GetKnownPlugins(
            IPersistentPluginScanCacheStorage<OpenFxScannedPluginInfo> persistentCacheStorage,
            bool useGpuRendering,
            Func<bool, bool, bool> isDeclaredBackendAvailable)
        {
            lock (lockObject)
            {
                if (cache is not null)
                {
                    if (cachedUseGpuRendering != useGpuRendering)
                    {
                        cache = ReevaluateGpuOnlySupport(cache, useGpuRendering, isDeclaredBackendAvailable);
                        cachedUseGpuRendering = useGpuRendering;
                    }
                    return cache;
                }
                return PersistentPluginScanCache.LoadPersistedPlugins(
                        persistentCacheStorage,
                        PersistentCacheFormatVersion,
                        "OFXバイナリ",
                        (x, path) => x with { BinaryPath = path },
                        IsValidScannedPlugin)
                    .Select(x => CreatePluginInfo(x, useGpuRendering, isDeclaredBackendAvailable))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        /// <summary>キャッシュ済みdescribe結果からGPU専用プラグインの対応可否だけを再評価する。</summary>
        public static IReadOnlyList<OpenFxPluginInfo>? ReevaluateCachedPlugins()
        {
            lock (lockObject)
            {
                var useGpuRendering = OpenFxSettings.Default.UseGpuRendering;
                if (cache is not null)
                {
                    cache = ReevaluateGpuOnlySupport(cache, useGpuRendering, OfxGpuRenderBackendFactory.IsDeclaredBackendAvailable);
                    cachedUseGpuRendering = useGpuRendering;
                    return cache;
                }
                if (incompleteCache is null)
                    return null;
                incompleteCache = ReevaluateGpuOnlySupport(incompleteCache, useGpuRendering, OfxGpuRenderBackendFactory.IsDeclaredBackendAvailable);
                incompleteCacheUseGpuRendering = useGpuRendering;
                return incompleteCache;
            }
        }

        static IReadOnlyList<OpenFxPluginInfo> ReevaluateGpuOnlySupport(
            IReadOnlyList<OpenFxPluginInfo> plugins,
            bool useGpuRendering,
            Func<bool, bool, bool> isDeclaredBackendAvailable)
            => plugins
                .Select(plugin =>
                {
                    if (plugin.UnsupportedReason is not null and not OpenFxUnsupportedReason.GpuOnly)
                        return plugin;
                    var gpuOnlyUnsupported = !plugin.GpuSupport.SupportsCPU
                        && !(useGpuRendering && isDeclaredBackendAvailable(
                            plugin.GpuSupport.SupportsCuda,
                            plugin.GpuSupport.SupportsOpenCLBuffer));
                    return plugin with
                    {
                        SupportsFilter = !gpuOnlyUnsupported && plugin.DeclaredSupportsFilter,
                        SupportsTransition = !gpuOnlyUnsupported && plugin.DeclaredSupportsTransition,
                        SupportsGenerator = !gpuOnlyUnsupported && plugin.DeclaredSupportsGenerator,
                        UnsupportedReason = gpuOnlyUnsupported ? OpenFxUnsupportedReason.GpuOnly : null,
                    };
                })
                .ToArray();

        /// <summary>
        /// スキャナープロセスによる隔離スキャン。壊れたプラグインがあってもYMM4本体は巻き込まれない。
        /// スキャナーEXEが見つからない・起動できない場合は失敗（null）を返し、呼び出し側はキャッシュしない。
        /// ユーザーのOFXバイナリを本体プロセスへロードするフォールバックは行わない。
        /// </summary>
        internal static PluginModuleScanResult<OpenFxScannedPluginInfo>? ScanIsolatedDetailed(string? scannerPath, IReadOnlyList<string> binaryPaths)
        {
            if (scannerPath is null)
            {
                Log.Default.Write($"{OpenFxScannerProcess.ExeName}が見つからないため、OFXスキャンを中止します。");
                return null;
            }
            try
            {
                return OpenFxScannerProcess.ScanDetailed(scannerPath, binaryPaths);
            }
            catch (Exception e)
            {
                Log.Default.Write("OFXスキャナープロセスの実行に失敗したため、OFXスキャンを中止します。", e);
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
        /// describe結果から対応可否を判定してプラグイン情報を作る。
        /// 対応外のプラグインも設定画面の一覧へ「非対応」として表示するため、除外せずUnsupportedReasonを設定して返す。
        /// ネイティブスキャナーが返す生のdescribe結果に対する共通判定
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
            bool? useGpuRendering = null,
            Func<bool, bool, bool>? isDeclaredBackendAvailable = null)
        {
            gpuSupport ??= OpenFxGpuSupport.Default;
            useGpuRendering ??= OpenFxSettings.Default.UseGpuRendering;
            isDeclaredBackendAvailable ??= OfxGpuRenderBackendFactory.IsDeclaredBackendAvailable;
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
                    && !(useGpuRendering.Value && isDeclaredBackendAvailable(
                        gpuSupport.SupportsCuda,
                        gpuSupport.SupportsOpenCLBuffer))
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

        static OpenFxPluginInfo CreatePluginInfo(
            OpenFxScannedPluginInfo plugin,
            bool? useGpuRendering = null,
            Func<bool, bool, bool>? isDeclaredBackendAvailable = null)
            => CreatePluginInfo(
                plugin.BinaryPath,
                plugin.Identifier,
                plugin.VersionMajor,
                plugin.VersionMinor,
                plugin.Label,
                plugin.Grouping,
                plugin.SupportedContexts,
                plugin.SupportedPixelDepths,
                plugin.IsSingleInstance,
                plugin.NeedsTemporalClipAccess,
                plugin.GpuSupport,
                useGpuRendering,
                isDeclaredBackendAvailable);

        static OpenFxGpuSupport GetGpuSupport(OfxPropertySet props)
            => new(
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenGLRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCudaRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCudaStreamSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenCLRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropOpenCLSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropMetalRenderSupported, "false"),
                props.GetStringOrDefault(OfxConstants.ImageEffectPropCPURenderSupported, "true"));

        static PluginModuleEnumerationResult EnumerateBinaries()
        {
            var roots = PersistentPluginScanCache.NormalizePaths(
                GetDefaultDirectories().Concat(OpenFxSettings.Default.AdditionalPluginDirectories),
                "OFX検索フォルダー");
            var existingRoots = roots.Where(Directory.Exists).ToArray();
            var rootsWithTransientErrors = new List<string>();
            var rootsWithPermanentErrors = new List<string>();
            var binaryPaths = PersistentPluginScanCache.NormalizePaths(
                EnumerateBinaryPaths(existingRoots, rootsWithTransientErrors, rootsWithPermanentErrors),
                "OFXバイナリ");
            return new PluginModuleEnumerationResult(binaryPaths, roots)
            {
                RootsWithTransientEnumerationErrors = rootsWithTransientErrors,
                RootsWithPermanentEnumerationErrors = rootsWithPermanentErrors,
            };
        }

        internal static string GetSignaturePath(string binaryPath)
        {
            var directory = Directory.GetParent(Path.GetFullPath(binaryPath));
            while (directory is not null)
            {
                if (directory.Name.EndsWith(".ofx.bundle", StringComparison.OrdinalIgnoreCase))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return binaryPath;
        }

        static bool IsStandaloneBinary(string binaryPath)
            => string.Equals(GetSignaturePath(binaryPath), binaryPath, StringComparison.OrdinalIgnoreCase);

        internal static IReadOnlyList<string> EnumerateBinaryPaths(
            IEnumerable<string> roots,
            ICollection<string>? rootsWithTransientErrors = null,
            ICollection<string>? rootsWithPermanentErrors = null)
        {
            var results = new List<string>();
            foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // OFXプラグインはバンドル形式（<名前>.ofx.bundle\Contents\Win64\<名前>.ofx）が標準。
                // 直接置かれた .ofx 単体ファイルも受け付ける
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
                            if (entry.EndsWith(".ofx.bundle", StringComparison.OrdinalIgnoreCase))
                            {
                                // バンドルは固定の相対パスを読むだけで再帰しないため、
                                // バンドル自体がジャンクション等のリンクでも受け付けてよい
                                string[] binaries;
                                try
                                {
                                    binaries = Directory.GetFiles(Path.Combine(entry, "Contents", "Win64"), "*.ofx");
                                }
                                catch (DirectoryNotFoundException)
                                {
                                    // Win64フォルダーを持たないバンドルは対象外（正常系）
                                    continue;
                                }
                                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                                {
                                    RecordEnumerationError(e, ref hasTransientEnumerationError, ref hasPermanentEnumerationError);
                                    continue;
                                }
                                results.AddRange(binaries);
                            }
                            // ジャンクション・シンボリックリンクは辿らない（親を指すリンクによる無限ループ防止）
                            else if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                directories.Push(entry);
                            }
                        }
                        else if (entry.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase))
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
