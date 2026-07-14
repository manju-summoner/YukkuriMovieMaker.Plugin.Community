using System;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3プラグインインスタンス1つ分のラッパー。
    /// スレッドセーフではない。呼び出し側で同一インスタンスへの並行アクセスを避けること。
    /// 複数スレッドから使用する場合はSyncRootで全呼び出しを直列化する
    /// （エディター用インスタンスはUIスレッドと音声フィードのワーカースレッドが共有する）。
    /// </summary>
    internal sealed unsafe class Vst3Plugin : IDisposable
    {
        /// <summary>
        /// 複数スレッドからの呼び出しを直列化するためのゲート
        /// </summary>
        public object SyncRoot { get; } = new();

        /// <summary>
        /// UIスレッド限定の制御系呼び出し（setActiveサイクル・状態取得等）をディスパッチャーへ委譲する。
        /// UIスレッド上・テスト環境ではそのまま実行する。
        /// SyncRoot等のロックを保持したまま呼ぶと、UIスレッド側がそのロックを待った場合に
        /// デッドロックするため、必ずロックの外から呼び、ロックは委譲先の中で取ること
        /// </summary>
        internal static void InvokeOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
                dispatcher.Invoke(action);
            else
                action();
        }

        readonly Vst3Native.MeterParameterChangeCallback meterParameterChangeCallback;
        IntPtr handle;
        Action<uint, double, long>? meterParameterChanged;

        internal Vst3Plugin(IntPtr handle)
        {
            this.handle = handle;
            meterParameterChangeCallback = OnMeterParameterChanged;
        }

        public bool IsDisposed => handle == IntPtr.Zero;

        /// <summary>
        /// サンプルレートと最大ブロックサイズを設定し、処理を開始できる状態にする。
        /// SetStateで状態を復元する場合はSetupより前に呼ぶこと。
        /// </summary>
        public void Setup(double sampleRate, int maxBlockSize)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Vst3Native.Ymm4Vst3PluginSetup(handle, sampleRate, maxBlockSize) == 0)
                throw new InvalidOperationException($"VST3プラグインのセットアップに失敗しました。sampleRate={sampleRate}");
        }

        /// <summary>
        /// プレーナー形式のL/Rバッファを処理する。projectTimeSamplesはフレーム（サンプル/チャンネル）単位。
        /// </summary>
        public bool Process(float[] inL, float[] inR, float[] outL, float[] outR, int numFrames, long projectTimeSamples)
            => Process(inL, inR, outL, outR, numFrames, projectTimeSamples, Vst3Transport.Default, true);

        public bool Process(
            float[] inL,
            float[] inR,
            float[] outL,
            float[] outR,
            int numFrames,
            long projectTimeSamples,
            in Vst3Transport transport,
            bool captureMeterParameters = true)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            fixed (float* pInL = inL, pInR = inR, pOutL = outL, pOutR = outR)
            {
                return Vst3Native.Ymm4Vst3PluginProcessWithTransport(
                    handle,
                    pInL, pInR, pOutL, pOutR,
                    numFrames,
                    projectTimeSamples,
                    transport.Tempo,
                    transport.TimeSignatureNumerator,
                    transport.TimeSignatureDenominator,
                    transport.IsTempoValid ? 1 : 0,
                    captureMeterParameters ? 1 : 0) != 0;
            }
        }

        /// <summary>
        /// エディタ操作をプロセッサ状態へ反映するための無音プロセスを1回実行する
        /// </summary>
        public void Pump()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3PluginPump(handle);
        }

        public int DrainEditorParameterChanges(Action<uint, double> onParameterChanged)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(onParameterChanged);
            Vst3Native.ParameterChangeCallback callback = (_, paramId, normalizedValue) =>
                onParameterChanged(paramId, normalizedValue);
            var count = Vst3Native.Ymm4Vst3PluginDrainEditorParameterChanges(handle, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return count;
        }

        public int DrainMeterParameterChanges(Action<uint, double, long> onParameterChanged)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentNullException.ThrowIfNull(onParameterChanged);
            meterParameterChanged = onParameterChanged;
            try
            {
                return Vst3Native.Ymm4Vst3PluginDrainMeterParameterChanges(
                    handle,
                    meterParameterChangeCallback,
                    IntPtr.Zero);
            }
            finally
            {
                meterParameterChanged = null;
                GC.KeepAlive(meterParameterChangeCallback);
            }
        }

        void OnMeterParameterChanged(IntPtr _, uint paramId, double normalizedValue, long samplePosition) =>
            meterParameterChanged?.Invoke(paramId, normalizedValue, samplePosition);

        /// <summary>
        /// プラグインが申告する処理遅延（フレーム数）。Setup後に取得する
        /// </summary>
        public int GetLatencySamples()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Vst3Native.Ymm4Vst3PluginGetLatencySamples(handle);
        }

        public int ConsumeRestartFlags()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Vst3Native.Ymm4Vst3PluginConsumeRestartFlags(handle);
        }

#if DEBUG
        internal void RequestRestartForTest(int flags)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3PluginRequestRestartForTest(handle, flags);
        }

        internal void PerformEditForTest(uint paramId, double normalizedValue)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3PluginPerformEditForTest(handle, paramId, normalizedValue);
        }


        internal double GetControllerParameterForTest(uint paramId)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Vst3Native.Ymm4Vst3PluginGetControllerParameterForTest(handle, paramId);
        }
#endif

        /// <summary>
        /// パラメータを正規化値で設定する。次のProcess/Pumpでプロセッサへ反映される
        /// </summary>
        public void SetParameter(uint paramId, double normalizedValue)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3PluginSetParameter(handle, paramId, normalizedValue);
        }

        /// <summary>
        /// output parameterをGUIへ表示するため、コントローラーだけを更新する。
        /// 音声プロセッサの入力パラメーターキューには追加しない。
        /// </summary>
        public bool SetControllerParameter(uint paramId, double normalizedValue)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Vst3Native.Ymm4Vst3PluginSetControllerParameter(handle, paramId, normalizedValue) != 0;
        }

        /// <summary>
        /// 内部バッファ（ディレイライン等）をリセットする。シーク時に呼ぶ
        /// </summary>
        public void Reset()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3PluginReset(handle);
        }

        public (byte[]? ComponentState, byte[]? ControllerState) GetState()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Vst3Native.Ymm4Vst3PluginGetState(handle, out var componentData, out var componentSize, out var controllerData, out var controllerSize) == 0)
                return (null, null);
            try
            {
                return (CopyAndFree(componentData, componentSize), CopyAndFree(controllerData, controllerSize));
            }
            finally
            {
                Vst3Native.Ymm4Vst3Free(componentData);
                Vst3Native.Ymm4Vst3Free(controllerData);
            }

            static byte[]? CopyAndFree(IntPtr data, int size)
            {
                if (data == IntPtr.Zero || size <= 0)
                    return null;
                var buffer = new byte[size];
                Marshal.Copy(data, buffer, 0, size);
                return buffer;
            }
        }

        public void SetState(byte[]? componentState, byte[]? controllerState)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (componentState is not { Length: > 0 } && controllerState is not { Length: > 0 })
                return;
            Vst3Native.Ymm4Vst3PluginSetState(
                handle,
                componentState, componentState?.Length ?? 0,
                controllerState, controllerState?.Length ?? 0);
        }

        /// <summary>
        /// エディタビューを作成する。GUIを持たないプラグインはnullを返す
        /// </summary>
        public Vst3View? CreateView()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var view = Vst3Native.Ymm4Vst3ViewCreate(handle);
            return view == IntPtr.Zero ? null : new Vst3View(view);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            var destroyingHandle = handle;
            handle = IntPtr.Zero;
            // 破棄（内部でsetActive(false)やterminateを呼ぶ）もVST3の規約どおりUIスレッド（メインスレッド）で行う。
            // 音声スレッドやバックグラウンドの停止処理から破棄されるため、UIスレッド外なら委譲する。
            // ハンドルは先に無効化済みで以後の呼び出しは来ないため、完了を待つ必要はない。
            // 優先度は再生成（EnsurePluginのDispatcher.Invoke＝Send）に追い越されないようSendにし、
            // 「旧インスタンスの破棄→再生成」の順序を保証する
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                Vst3Native.Ymm4Vst3PluginDestroy(destroyingHandle);
            }
            else if (!dispatcher.HasShutdownStarted)
            {
                dispatcher.InvokeAsync(
                    () => Vst3Native.Ymm4Vst3PluginDestroy(destroyingHandle),
                    System.Windows.Threading.DispatcherPriority.Send);
            }
            // アプリ終了中（Dispatcherシャットダウン後）はUIスレッドへ委譲できない。
            // 別スレッドでのネイティブ破棄は終了時クラッシュの原因になるため、破棄せず残して
            // プロセス終了でOSに回収させる（モジュールはピン留め済みで、いずれにせよ終了まで残る）
        }
    }

    internal readonly record struct Vst3Transport(
        double Tempo,
        int TimeSignatureNumerator,
        int TimeSignatureDenominator,
        bool IsTempoValid)
    {
        public static Vst3Transport Default { get; } = new(120, 4, 4, true);
    }

    /// <summary>
    /// VST3プラグインのエディタビュー。UIスレッドから操作すること。
    /// </summary>
    internal sealed class Vst3View : IDisposable
    {
        IntPtr handle;
        Vst3Native.ViewResizeCallback? resizeCallback;

        internal Vst3View(IntPtr handle)
        {
            this.handle = handle;
        }

        public bool IsDisposed => handle == IntPtr.Zero;

        public (int Width, int Height) GetSize()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Vst3Native.Ymm4Vst3ViewGetSize(handle, out var width, out var height) == 0)
                return (0, 0);
            return (width, height);
        }

        public bool CanResize
        {
            get
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return Vst3Native.Ymm4Vst3ViewCanResize(handle) != 0;
            }
        }

        /// <summary>
        /// HWNDへアタッチする。onResizeRequestedはプラグイン都合のリサイズ要求時に呼ばれる
        /// </summary>
        public bool Attach(IntPtr hwnd, Action<int, int>? onResizeRequested)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            // GC回収を防ぐためデリゲートをフィールドに保持する
            resizeCallback = onResizeRequested is null
                ? null
                : (_, width, height) => onResizeRequested(width, height);
            return Vst3Native.Ymm4Vst3ViewAttach(handle, hwnd, resizeCallback, IntPtr.Zero) != 0;
        }

        /// <summary>
        /// 高DPI用のコンテンツスケールをビューへ通知する。プラグインが対応しているとtrueを返す
        /// </summary>
        public bool SetContentScale(double scaleFactor)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Vst3Native.Ymm4Vst3ViewSetContentScale(handle, (float)scaleFactor) != 0;
        }

        /// <summary>
        /// IPlugViewContentScaleSupportを実装している（高DPI対応の）プラグインかどうか
        /// </summary>
        public bool IsContentScaleSupported
        {
            get
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return Vst3Native.Ymm4Vst3ViewIsContentScaleSupported(handle) != 0;
            }
        }


        /// <summary>
        /// ウィンドウ都合のリサイズをビューへ通知する。制約適用後の実サイズを返す
        /// </summary>
        public (int Width, int Height) OnSize(int width, int height)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Vst3Native.Ymm4Vst3ViewOnSize(handle, ref width, ref height);
            return (width, height);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            Vst3Native.Ymm4Vst3ViewDestroy(handle);
            handle = IntPtr.Zero;
            resizeCallback = null;
        }
    }
}
