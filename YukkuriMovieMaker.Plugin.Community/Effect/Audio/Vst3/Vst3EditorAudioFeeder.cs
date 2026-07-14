using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// タイムラインの再生位置に追従して、エフェクトへの入力音声（UpTo選択）をエディター用プラグインへ処理させる。
    /// プラグインGUIの波形・アナライザー等が再生・シークにあわせてリアルタイムに更新されるようにする。
    /// 位置の基準はメーター転送と同じくアイテム相対の再生位置（IEditorInfo.ItemPosition）のため、
    /// 実際に聴こえている音と表示が同期する。
    /// 音声の読み取りとProcessは専用のワーカースレッドで行い、処理の重いプラグインでもUIをブロックしない。
    /// プラグインへの呼び出しはチャンク単位でSyncRootを取得して直列化し、UI側の操作が割り込める粒度を保つ
    /// </summary>
    internal sealed class Vst3EditorAudioFeeder : IDisposable
    {
        /// <summary>
        /// 1回のProcessで処理する最大フレーム数。シーク時はこの分だけ手前から処理して表示を追従させ、
        /// 再生がこれ以上遅れた場合は読み飛ばして実時間へ追いつく
        /// </summary>
        const int MaxChunkFrames = 8192;

        /// <summary>
        /// 目標位置が動かない場合でも申告レイテンシの変化（kLatencyChanged）を検出するための起床間隔
        /// </summary>
        static readonly TimeSpan IdleWakeInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// この時間内にProcessを実行していれば「フィード中」とみなす。
        /// フィード中は無音Pumpとタイムライン履歴のメーター転送が不要になる
        /// </summary>
        static readonly TimeSpan ActiveFeedWindow = TimeSpan.FromMilliseconds(100);

        // IEditorInfoは生成時点の値を固定したスナップショットのため、再生位置へ追従できるよう常に最新を取得する
        readonly Func<IEditorInfo?> getEditorInfo;
        readonly Vst3AudioEffect effect;
        readonly Vst3Plugin plugin;
        readonly int hz;
        readonly Thread workerThread;
        readonly AutoResetEvent wakeEvent = new(false);
        readonly Action<uint, double, long> captureMeterValue;

        // フィード直後のメーター値（output parameter）。ワーカーが書き、UIスレッドがコントローラーへ反映する
        readonly ConcurrentDictionary<uint, double> pendingMeterValues = new();

        // UIスレッド→ワーカーの受け渡し（sync配下）。
        // IEditorInfo・エフェクトのプロパティ・音声ソースの生成はUIスレッド専有のため、ワーカーはここ経由でのみ受け取る
        readonly object sync = new();
        IAudioStream? source;
        long sourceDuration;
        bool isSourceUnavailable;
        bool isWakeEventDisposed;
        long displayTarget = -1;
        Vst3Transport transport = Vst3Transport.Default;

        // ワーカースレッド専有
        long nextPosition = -1;
        long lastDisplayTarget = -1;
        long latencyLeadSamples;
        float[] readBuffer = [];
        float[] bufferL = [];
        float[] bufferR = [];

        volatile bool isDisposed;
        long lastFedTimestamp;

        // UIティックがゲート内でサンプリングした申告レイテンシ（フレーム数）。
        // getLatencySamples等の制御系呼び出しはVST3の規約上UIスレッド限定のため、ワーカーはこの値を参照する
        volatile int sampledLatencyFrames = -1;

        public Vst3EditorAudioFeeder(Func<IEditorInfo?> getEditorInfo, Vst3AudioEffect effect, Vst3Plugin plugin, int hz)
        {
            this.getEditorInfo = getEditorInfo;
            this.effect = effect;
            this.plugin = plugin;
            this.hz = hz;
            captureMeterValue = (paramId, normalizedValue, _) => pendingMeterValues[paramId] = normalizedValue;
            workerThread = new Thread(FeedLoop) { IsBackground = true, Name = "VST3 EditorAudioFeed" };
            workerThread.Start();
        }

        /// <summary>
        /// 直近にProcessを実行していればtrue（＝無音Pumpとタイムライン履歴のメーター転送をスキップしてよい）
        /// </summary>
        public bool IsActivelyFeeding =>
            Stopwatch.GetElapsedTime(Volatile.Read(ref lastFedTimestamp)) < ActiveFeedWindow;

        /// <summary>
        /// 現在のタイムライン位置とトランスポート情報をワーカーへ通知する。UIスレッドから定期的に呼ぶ
        /// </summary>
        public void UpdateTarget()
        {
            if (isDisposed)
                return;
            var editorInfo = getEditorInfo();
            if (editorInfo is null)
                return;
            if (!EnsureSource(editorInfo))
                return;

            var target = (long)(editorInfo.ItemPosition.Time.TotalSeconds * hz) * 2;
            var newTransport = new Vst3Transport(effect.Tempo, effect.TimeSignatureNumerator, effect.TimeSignatureDenominator, effect.IsTempoSyncEnabled);
            bool changed;
            lock (sync)
            {
                target = Math.Clamp(target - target % 2, 0, sourceDuration);
                changed = displayTarget != target || transport != newTransport;
                if (changed)
                {
                    displayTarget = target;
                    transport = newTransport;
                }
            }
            if (changed)
            {
                lock (sync)
                {
                    if (!isWakeEventDisposed)
                        wakeEvent.Set();
                }
            }
        }

        /// <summary>
        /// 申告レイテンシをサンプリングしてワーカーへ引き渡す。
        /// UIスレッドからplugin.SyncRootを保持した状態で定期的に呼ぶこと
        /// （制御系呼び出しはUIスレッド限定のため、ワーカー自身では取得しない）
        /// </summary>
        public void SampleLatency()
        {
            if (plugin.IsDisposed)
                return;
            sampledLatencyFrames = Math.Max(0, plugin.GetLatencySamples());
        }

        /// <summary>
        /// フィード直後に控えたメーター値をコントローラーへ反映する。
        /// UIスレッドからplugin.SyncRootを保持した状態で呼ぶこと
        /// </summary>
        public void ApplyMeterValues()
        {
            foreach (var (paramId, _) in pendingMeterValues)
            {
                if (!pendingMeterValues.TryRemove(paramId, out var normalizedValue))
                    continue;
                plugin.SetControllerParameter(paramId, normalizedValue);
            }
        }

        void FeedLoop()
        {
            try
            {
                while (!isDisposed)
                {
                    wakeEvent.WaitOne(IdleWakeInterval);
                    if (isDisposed)
                        return;
                    try
                    {
                        // 目標位置に追いつくまで処理する。チャンクごとにロックを解放するため、UI側の操作は合間に割り込める
                        while (!isDisposed && FeedChunk())
                        {
                        }
                    }
                    catch (Exception e)
                    {
                        // アイテムの状態変化等で読み取りに失敗したら、以後はPumpのみで動作する
                        Log.Default.Write("VST3エディターへの音声転送に失敗しました。", e);
                        MarkSourceUnavailable();
                    }
                }
            }
            finally
            {
                // 音声ソースの読み取りとイベントの使用はワーカー専有のため、終了時に自身で破棄する
                MarkSourceUnavailable();
                lock (sync)
                {
                    isWakeEventDisposed = true;
                    wakeEvent.Dispose();
                }
                pendingMeterValues.Clear();
            }
        }

        /// <summary>
        /// 前回から現在のタイムライン位置までの入力音声を最大1チャンク処理する。
        /// 追いついている・処理できない場合はfalseを返す
        /// </summary>
        bool FeedChunk()
        {
            IAudioStream? stream;
            long displayTargetLocal;
            Vst3Transport transportLocal;
            long duration;
            lock (sync)
            {
                stream = source;
                displayTargetLocal = displayTarget;
                transportLocal = transport;
                duration = sourceDuration;
            }
            if (stream is null || displayTargetLocal < 0)
                return false;

            // レイテンシはUIティックがサンプリングした値を使う。初回サンプリングまでは処理しない
            var latencyFrames = sampledLatencyFrames;
            if (latencyFrames < 0)
                return false;
            var latencySamples = latencyFrames * 2L;

            // 実再生と同様に、申告レイテンシ分だけ入力を先行させて表示位置と実際に聴こえる音を一致させる。
            // 終端付近では超過分を無音として処理し、遅延出力を吐き切る（論理位置は終端を超えられる）
            var target = displayTargetLocal + latencySamples;
            target -= target % 2;
            if (target == nextPosition && latencySamples == latencyLeadSamples)
                return false;

            // シーク（後退・大きなジャンプ）と申告レイテンシの変更（kLatencyChanged）は、
            // 内部状態をリセットして直前から処理し直す。
            // 判定はフィード位置との距離ではなく表示位置の変化で行う。距離基準にすると、
            // 申告レイテンシが1チャンクを超えるプラグインでプライム中の残量が毎回
            // ジャンプ扱いになり、リセットと巻き戻しを繰り返して補正位置へ到達できない
            var isDiscontinuous = nextPosition < 0
                || latencySamples != latencyLeadSamples
                || displayTargetLocal < lastDisplayTarget
                || displayTargetLocal - lastDisplayTarget > MaxChunkFrames * 2L
                // フィードが実時間に追いつけず遅れが積み上がった場合は読み飛ばして追いつく
                || target - nextPosition > latencySamples + MaxChunkFrames * 2L * 2;
            lastDisplayTarget = displayTargetLocal;
            if (isDiscontinuous)
            {
                latencyLeadSamples = latencySamples;
                // 非連続な音声を投入する前に内部状態（ディレイライン等）をリセットし、移動前の音声の混入を防ぐ。
                // リセット（内部でsetActiveサイクル）は制御系呼び出しのため、VST3の規約どおりUIスレッドで実行する。
                // UIスレッドはワーカーを同期待ちしないため、ここでの待機はデッドロックしない
                InvokeOnUiThread(() =>
                {
                    lock (plugin.SyncRoot)
                    {
                        if (!plugin.IsDisposed)
                            plugin.Reset();
                    }
                });
                // 高レイテンシのプラグインでも遅延ラインが満たされるよう、申告レイテンシ＋1チャンク分手前からプライムする
                nextPosition = Math.Max(0, target - latencyLeadSamples - MaxChunkFrames * 2L);
                nextPosition -= nextPosition % 2;
            }

            var count = (int)Math.Min(target - nextPosition, MaxChunkFrames * 2L);
            count -= count % 2;
            if (count <= 0)
            {
                nextPosition = target;
                return false;
            }

            if (readBuffer.Length < count)
                readBuffer = new float[count];
            var available = (int)Math.Clamp(duration - nextPosition, 0, count);
            available -= available % 2;
            var read = 0;
            if (available > 0)
            {
                if (stream.Position != nextPosition)
                    stream.Seek(nextPosition);
                read = stream.Read(readBuffer, 0, available);
                read -= read % 2;
                read = Math.Max(0, read);
            }
            // 終端を超えた分（レイテンシの吐き切り）は無音で埋める
            Array.Clear(readBuffer, read, count - read);

            var frames = count / 2;
            if (bufferL.Length < frames)
            {
                bufferL = new float[frames];
                bufferR = new float[frames];
            }
            for (var i = 0; i < frames; i++)
            {
                bufferL[i] = readBuffer[i * 2];
                bufferR[i] = readBuffer[i * 2 + 1];
            }

            bool succeeded;
            lock (plugin.SyncRoot)
            {
                if (plugin.IsDisposed)
                    return false;
                succeeded = plugin.Process(
                    bufferL, bufferR, bufferL, bufferR,
                    frames,
                    nextPosition / 2,
                    transportLocal);
                // 処理直後の出力パラメーター（メーター等）を控え、UIスレッドがコントローラーへ反映する
                plugin.DrainMeterParameterChanges(captureMeterValue);
            }
            nextPosition += count;
            if (succeeded)
                Volatile.Write(ref lastFedTimestamp, Stopwatch.GetTimestamp());
            return true;
        }

        /// <summary>
        /// 音声ソースを準備する。UIスレッドからのみ呼ぶ（生成がSceneへ触るため）
        /// </summary>
        bool EnsureSource(IEditorInfo editorInfo)
        {
            lock (sync)
            {
                if (source is not null)
                    return true;
                if (isSourceUnavailable)
                    return false;
            }
            IAudioStream? created = null;
            try
            {
                created = editorInfo.CreateItemAudioSource(new ItemAudioSourceCreationParameter(AudioEffectSelection.UpTo(effect)));
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3エディター用の音声ソース作成に失敗しました。", e);
            }
            // 音声アイテム以外（null）や作成失敗、サンプルレート不一致の場合は以後試さない
            if (created is null || created.Hz != hz)
            {
                created?.Dispose();
                lock (sync)
                    isSourceUnavailable = true;
                return false;
            }
            lock (sync)
            {
                if (isDisposed)
                {
                    created.Dispose();
                    return false;
                }
                source = created;
                sourceDuration = created.Duration;
                return true;
            }
        }

        void MarkSourceUnavailable()
        {
            lock (sync)
            {
                source?.Dispose();
                source = null;
                isSourceUnavailable = true;
            }
        }

        /// <summary>
        /// UIスレッド限定の制御系呼び出しをディスパッチャーへ委譲する（UIスレッド上・テスト環境ではそのまま実行）
        /// </summary>
        static void InvokeOnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
                dispatcher.Invoke(action);
            else
                action();
        }

        /// <summary>
        /// ワーカースレッドへ停止を要求する。Joinによる完了待ちはしない——
        /// ワーカーは上流エフェクトのプラグイン初期化（Dispatcher.Invoke）でUIスレッドの応答を
        /// 待つことがあり、UIスレッドがここでJoinすると相互待ちでデッドロックするため。
        /// プラグインの破棄はSyncRootで直列化されており（Vst3EditorSession参照）、
        /// ワーカーは破棄済みプラグインに触れる前にゲート内のIsDisposedチェックで自然終了する
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            lock (sync)
            {
                if (!isWakeEventDisposed)
                    wakeEvent.Set();
            }
        }
    }
}
