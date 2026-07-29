using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// .ofx バイナリ（1つのDLL）のラッパー。プラグインの列挙と OfxPlugin 構造体の取得を担う。
    ///
    /// 一度ロードしたバイナリはプロセス終了までアンロードしない（VST3ホスティングと同じ方針。
    /// アンロード後にプラグインが登録したままのコールバックが無効な飛び先になる事故を避けるため）。
    /// このため Load は静的キャッシュを返す。
    /// </summary>
    internal sealed unsafe class OfxPluginBinary
    {
        static readonly object cacheSync = new();
        static readonly Dictionary<string, OfxPluginBinary> cache = new(StringComparer.OrdinalIgnoreCase);

        readonly nint library;
        readonly delegate* unmanaged[Cdecl]<int> getNumberOfPlugins;
        readonly delegate* unmanaged[Cdecl]<int, OfxPluginNative*> getPlugin;

        public string Path { get; }

        /// <summary>OfxSetHost（任意エクスポート）が kOfxStatFailed を返した場合 false（このバイナリはスキップする）</summary>
        public bool IsAvailable { get; }

        OfxPluginBinary(string path)
        {
            Path = path;
            if (!NativeLibrary.TryLoad(path, out library))
                throw new InvalidOperationException($"OFXバイナリを読み込めませんでした。path={path}");
            if (!NativeLibrary.TryGetExport(library, "OfxGetNumberOfPlugins", out var getCountPtr)
                || !NativeLibrary.TryGetExport(library, "OfxGetPlugin", out var getPluginPtr))
            {
                // 非OFXのDLLをキャッシュ登録前に弾く経路。ここではまだプラグインへ制御が渡っていないため解放してよい
                NativeLibrary.Free(library);
                throw new InvalidOperationException($"OFXのエクスポート関数が見つかりません。path={path}");
            }
            getNumberOfPlugins = (delegate* unmanaged[Cdecl]<int>)getCountPtr;
            getPlugin = (delegate* unmanaged[Cdecl]<int, OfxPluginNative*>)getPluginPtr;

            // OfxSetHost はOFX 1.4以降の任意エクスポート。列挙より先に呼ぶ決まり。
            // kOfxStatFailed が返った場合は「このホスト向けではない」ため黙ってスキップする
            IsAvailable = true;
            if (NativeLibrary.TryGetExport(library, "OfxSetHost", out var setHostPtr))
            {
                var setHost = (delegate* unmanaged[Cdecl]<nint, int>)setHostPtr;
                var status = setHost(OfxHostDescriptor.HostStructPointer);
                if (status == OfxStatus.Failed)
                    IsAvailable = false;
            }
        }

        /// <summary>
        /// バイナリをロードして返す（同一パスはキャッシュを返す）
        /// </summary>
        public static OfxPluginBinary Load(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            lock (cacheSync)
            {
                if (cache.TryGetValue(fullPath, out var cached))
                    return cached;
                var binary = new OfxPluginBinary(fullPath);
                cache[fullPath] = binary;
                return binary;
            }
        }

        /// <summary>
        /// バイナリ内の画像エフェクトプラグインを列挙する。
        /// 対応外のAPI・バージョンのプラグインは除外する。
        /// ラッパーはバイナリごとにキャッシュする（都度生成するとロード状態・describeキャッシュが失われ、
        /// 同じネイティブプラグインへ kOfxActionLoad を繰り返し送ってしまうため）。
        /// </summary>
        public IReadOnlyList<OfxImageEffectPlugin> EnumerateImageEffectPlugins()
        {
            lock (pluginsSync)
            {
                return cachedPlugins ??= EnumerateImageEffectPluginsCore();
            }
        }

        readonly object pluginsSync = new();
        IReadOnlyList<OfxImageEffectPlugin>? cachedPlugins;

        IReadOnlyList<OfxImageEffectPlugin> EnumerateImageEffectPluginsCore()
        {
            var result = new List<OfxImageEffectPlugin>();
            if (!IsAvailable)
                return result;
            var count = getNumberOfPlugins();
            for (var i = 0; i < count; i++)
            {
                var plugin = getPlugin(i);
                if (plugin is null)
                    continue;
                var api = Marshal.PtrToStringUTF8(plugin->pluginApi);
                if (api != OfxConstants.ImageEffectPluginApi)
                {
                    OfxHostLog.Debug($"対応外のプラグインAPIをスキップ: {api} ({Path})");
                    continue;
                }
                if (plugin->apiVersion != OfxConstants.ImageEffectPluginApiVersion)
                {
                    OfxHostLog.Debug($"対応外のAPIバージョンをスキップ: {plugin->apiVersion} ({Path})");
                    continue;
                }
                var identifier = Marshal.PtrToStringUTF8(plugin->pluginIdentifier);
                if (string.IsNullOrEmpty(identifier))
                    continue;
                // 壊れたバイナリの部分初期化された構造体でNULL関数を呼ばないよう検査する
                if (plugin->setHost == 0 || plugin->mainEntry == 0)
                {
                    OfxHostLog.Info($"setHost/mainEntryが未設定のプラグインをスキップ: {identifier} ({Path})");
                    continue;
                }
                result.Add(new OfxImageEffectPlugin(this, plugin, identifier));
            }
            return result;
        }
    }

    /// <summary>
    /// バイナリ内の画像エフェクトプラグイン1つ分のラッパー。
    /// setHost → kOfxActionLoad → kOfxActionDescribe → kOfxImageEffectActionDescribeInContext の
    /// アクション駆動を担う。
    /// </summary>
    internal sealed unsafe class OfxImageEffectPlugin
    {
        readonly OfxPluginBinary binary;
        readonly OfxPluginNative* plugin;
        readonly object sync = new();
        bool isLoaded;
        OfxEffectDescriptor? globalDescriptor;
        readonly Dictionary<string, OfxEffectDescriptor> contextDescriptors = [];

        public string Identifier { get; }
        public uint VersionMajor => plugin->pluginVersionMajor;
        public uint VersionMinor => plugin->pluginVersionMinor;
        public string BinaryPath => binary.Path;

        internal OfxImageEffectPlugin(OfxPluginBinary binary, OfxPluginNative* plugin, string identifier)
        {
            this.binary = binary;
            this.plugin = plugin;
            Identifier = identifier;
        }

        /// <summary>
        /// mainEntry を呼ぶ。action はUTF8へ変換して渡す。
        /// </summary>
        public int CallAction(string action, nint handle, nint inArgs, nint outArgs)
        {
            var actionUtf8 = Marshal.StringToCoTaskMemUTF8(action);
            try
            {
                var mainEntry = (delegate* unmanaged[Cdecl]<nint, nint, nint, nint, int>)plugin->mainEntry;
                return mainEntry(actionUtf8, handle, inArgs, outArgs);
            }
            catch (ArithmeticException e)
            {
                // プラグイン内の整数ゼロ除算等のハードウェア例外はCLRがマネージド例外へ変換して
                // ここへ届く（実測: openfx-misc ColorBarsのExtent=Size・サイズ0）。
                // ホスト例外として漏らさず、アクションの失敗ステータスへ変換する
                OfxHostLog.Info($"プラグイン内で演算例外が発生しました。plugin={Identifier} action={action}: {e.Message}");
                return OfxStatus.Failed;
            }
            finally
            {
                Marshal.FreeCoTaskMem(actionUtf8);
            }
        }

        /// <summary>
        /// setHost と kOfxActionLoad を（未実行なら）実行する
        /// </summary>
        public void EnsureLoaded()
        {
            lock (sync)
            {
                if (isLoaded)
                    return;
                var setHost = (delegate* unmanaged[Cdecl]<nint, void>)plugin->setHost;
                setHost(OfxHostDescriptor.HostStructPointer);
                var status = CallAction(OfxConstants.ActionLoad, 0, 0, 0);
                if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                    throw new InvalidOperationException($"kOfxActionLoad が失敗しました。plugin={Identifier} status={status}");
                isLoaded = true;
            }
        }

        /// <summary>
        /// kOfxActionDescribe を実行してグローバルディスクリプタを得る（結果はキャッシュされる）
        /// </summary>
        public OfxEffectDescriptor Describe()
        {
            lock (sync)
            {
                if (globalDescriptor is not null)
                    return globalDescriptor;
            }
            EnsureLoaded();
            lock (sync)
            {
                if (globalDescriptor is not null)
                    return globalDescriptor;
                var descriptor = new OfxEffectDescriptor(binary.Path, Identifier);
                var status = CallAction(OfxConstants.ActionDescribe, descriptor.Handle, 0, 0);
                if (status is not OfxStatus.OK)
                {
                    descriptor.Dispose();
                    throw new InvalidOperationException($"kOfxActionDescribe が失敗しました。plugin={Identifier} status={status}");
                }
                globalDescriptor = descriptor;
                return descriptor;
            }
        }

        /// <summary>
        /// kOfxImageEffectActionDescribeInContext を実行してコンテキスト別ディスクリプタを得る
        /// （結果はキャッシュされる）
        /// </summary>
        public OfxEffectDescriptor DescribeInContext(string context)
        {
            var global = Describe();
            lock (sync)
            {
                if (contextDescriptors.TryGetValue(context, out var cached))
                    return cached;
                var descriptor = new OfxEffectDescriptor(global, context);
                using var inArgs = new OfxPropertySet { DebugName = "describeInContext.inArgs" };
                inArgs.SetString(OfxConstants.ImageEffectPropContext, context);
                var status = CallAction(OfxConstants.ImageEffectActionDescribeInContext, descriptor.Handle, inArgs.Handle, 0);
                // describeInContextは必須アクションで、成功を示す戻り値は kOfxStatOK のみ。
                // ReplyDefault（未処理）を成功扱いすると、クリップもパラメータもない空ディスクリプタが
                // キャッシュされ、選択後の描画が失敗し続ける
                if (status is not OfxStatus.OK)
                {
                    descriptor.Dispose();
                    throw new InvalidOperationException($"kOfxImageEffectActionDescribeInContext が失敗しました。plugin={Identifier} context={context} status={status}");
                }
                contextDescriptors[context] = descriptor;
                return descriptor;
            }
        }
    }
}
