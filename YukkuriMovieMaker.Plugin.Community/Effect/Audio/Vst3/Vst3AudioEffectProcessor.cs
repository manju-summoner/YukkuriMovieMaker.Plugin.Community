using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3プラグインで音声を加工するプロセッサ。
    /// プラグインの読み込みに失敗した場合は素通しで動作する。
    /// プラグインが申告する処理遅延（getLatencySamples）は、開始・シーク時にレイテンシ分の
    /// 入力を先行投入して出力を読み捨てることで補正する（以後、入力は出力より先行して読む）。
    /// </summary>
    internal class Vst3AudioEffectProcessor : AudioEffectProcessorBase
    {
        const int MaxBlockFrames = 4096;

        readonly Vst3AudioEffect item;
        readonly Vst3ParameterSubscription parameterSubscription;
        readonly Vst3MeterPublisher meterPublisher;
        readonly Action<uint, double, long> meterParameterChanged;
        Vst3Plugin? plugin;
        bool isLoadFailed;
        bool isPrimed;
        int latencyFrames;
        float[] bufferL = [];
        float[] bufferR = [];
        float[] primeBuffer = [];
        float[] dryBuffer = [];
        float[] dryDelayBuffer = [];
        int dryDelayIndex;
        byte[]? pendingComponentState;
        byte[]? pendingControllerState;
        bool hasPendingState;
        long position;

        public Vst3AudioEffectProcessor(Vst3AudioEffect item)
        {
            this.item = item;
            parameterSubscription = item.ParameterChannel.Subscribe();
            disposer.Collect(parameterSubscription);
            meterPublisher = item.MeterChannel.CreatePublisher();
            disposer.Collect(meterPublisher);
            meterParameterChanged = OnMeterParameterChanged;
        }

        public override int Hz => Input!.Hz;
        public override long Duration => Input!.Duration;

        protected override int read(float[] destBuffer, int offset, int count)
        {
            EnsurePlugin();
            if (plugin is null)
            {
                var readCount = Input!.Read(destBuffer, offset, count);
                if (readCount > 0)
                    position += readCount;
                return readCount;
            }

            parameterSubscription.ApplyTo(plugin);

            // 入力はレイテンシ分先読みしているため、出力可能量は自身の位置基準で決める
            var outCount = (int)Math.Min(count, Duration - position);
            outCount -= outCount % 2;
            if (outCount <= 0)
                return 0;

            PrimeLatency();

            // 入力を読み、末尾に届いた分は無音でパディングしてプラグインの遅延分を吐き切らせる
            var inputRead = Math.Max(0, Input!.Read(destBuffer, offset, outCount));
            Array.Clear(destBuffer, offset + inputRead, outCount - inputRead);

            if (dryBuffer.Length < outCount)
                dryBuffer = new float[outCount];
            if (dryDelayBuffer.Length > 0)
            {
                for (var i = 0; i < outCount; i++)
                {
                    dryBuffer[i] = dryDelayBuffer[dryDelayIndex];
                    dryDelayBuffer[dryDelayIndex] = destBuffer[offset + i];
                    dryDelayIndex = (dryDelayIndex + 1) % dryDelayBuffer.Length;
                }
            }
            else
            {
                Array.Copy(destBuffer, offset, dryBuffer, 0, outCount);
            }

            var frames = outCount / 2;
            EnsureBuffers(frames);
            for (var i = 0; i < frames; i++)
            {
                bufferL[i] = destBuffer[offset + i * 2];
                bufferR[i] = destBuffer[offset + i * 2 + 1];
            }

            if (plugin.Process(bufferL, bufferR, bufferL, bufferR, frames, (position + latencyFrames * 2L) / 2, CreateTransport()))
            {
                plugin.DrainMeterParameterChanges(meterParameterChanged);
                var totalFrames = Duration / 2;
                var startFrame = position / 2;
                for (var i = 0; i < frames; i++)
                {
                    // 始点終点の線形補間ではブロック内のキーフレームが失われるため、フレームごとに評価する
                    var mix = (float)(item.Mix.GetValue(startFrame + i, totalFrames, Hz) * 0.01);
                    destBuffer[offset + i * 2] = dryBuffer[i * 2] * (1 - mix) + bufferL[i] * mix;
                    destBuffer[offset + i * 2 + 1] = dryBuffer[i * 2 + 1] * (1 - mix) + bufferR[i] * mix;
                }
                HandleRestartFlags(position + outCount);
            }
            else
            {
                // 入力はレイテンシ分先読み済みでdestBufferの中身は現在位置より未来の音声のため、
                // そのまま返すと時間軸がずれる。プラグインを切り離し、入力位置を出力位置へ
                // 戻したうえで素通しへ移行する
                Log.Default.Write($"VST3プラグインの音声処理に失敗しました。素通しに切り替えます。path={item.PluginPath}");
                disposer.RemoveAndDispose(ref plugin);
                isLoadFailed = true;
                Input!.Seek(position);
                var readCount = Math.Max(0, Input.Read(destBuffer, offset, outCount));
                if (readCount > 0)
                    position += readCount;
                return readCount;
            }

            position += outCount;
            return outCount;
        }

        protected override void seek(long position)
        {
            this.position = position;
            if (plugin is { } resettingPlugin)
            {
                // リセット（内部でsetActiveサイクル）は制御系呼び出しのため、VST3の規約どおりUIスレッドで実行する。
                // シークは音声スレッド（再生速度変更・書き出し・レベルメーター読み）からも呼ばれる
                Vst3Plugin.InvokeOnUiThread(() =>
                {
                    if (!resettingPlugin.IsDisposed)
                        resettingPlugin.Reset();
                });
            }
            meterPublisher.Reset(position / 2, Hz);
            isPrimed = false;
            dryDelayIndex = 0;
            Input!.Seek(position);
        }

        /// <summary>
        /// レイテンシ分の入力を先行投入し、出力を読み捨てて時間軸を揃える
        /// </summary>
        void PrimeLatency()
        {
            if (isPrimed)
                return;
            isPrimed = true;
            if (plugin is null)
                return;

            dryDelayIndex = 0;
            if (latencyFrames <= 0)
            {
                dryDelayBuffer = [];
                return;
            }

            var delaySamples = latencyFrames * 2;
            if (dryDelayBuffer.Length == delaySamples)
                Array.Clear(dryDelayBuffer);
            else
                dryDelayBuffer = new float[delaySamples];

            var chunkFrames = Math.Min(latencyFrames, MaxBlockFrames);
            EnsureBuffers(chunkFrames);
            if (primeBuffer.Length < chunkFrames * 2)
                primeBuffer = new float[chunkFrames * 2];

            var processedFrames = 0;
            while (processedFrames < latencyFrames)
            {
                var frames = Math.Min(latencyFrames - processedFrames, chunkFrames);
                var readCount = Math.Max(0, Input!.Read(primeBuffer, 0, frames * 2));
                Array.Clear(primeBuffer, readCount, frames * 2 - readCount);
                Array.Copy(primeBuffer, 0, dryDelayBuffer, processedFrames * 2, frames * 2);
                for (var i = 0; i < frames; i++)
                {
                    bufferL[i] = primeBuffer[i * 2];
                    bufferR[i] = primeBuffer[i * 2 + 1];
                }
                plugin.Process(
                    bufferL,
                    bufferR,
                    bufferL,
                    bufferR,
                    frames,
                    (position + processedFrames * 2L) / 2,
                    CreateTransport(),
                    false);
                processedFrames += frames;
            }
        }

        Vst3Transport CreateTransport() => new(
            item.Tempo,
            item.TimeSignatureNumerator,
            item.TimeSignatureDenominator,
            item.IsTempoSyncEnabled);

        void OnMeterParameterChanged(uint paramId, double normalizedValue, long samplePosition) =>
            meterPublisher.Publish(paramId, normalizedValue, samplePosition - latencyFrames, Hz);

        void EnsureBuffers(int frames)
        {
            if (bufferL.Length < frames)
            {
                bufferL = new float[frames];
                bufferR = new float[frames];
            }
        }

        void EnsurePlugin()
        {
            if (plugin is not null || isLoadFailed)
                return;
            if (string.IsNullOrEmpty(item.PluginPath) || string.IsNullOrEmpty(item.ClassId))
            {
                isLoadFailed = true;
                return;
            }
            try
            {
                // VST3の規約どおり、モジュールロード〜セットアップはUIスレッド（メインスレッド）で行う。
                // JUCE等のフレームワークは最初にインスタンス生成されたスレッドを「メッセージスレッド」として
                // 固定するため、音声スレッドで生成するとエディター表示（UIスレッドのattach）が
                // メッセージスレッドの応答待ちで永久にフリーズする。
                // ※逆方向のデッドロックを防ぐため、UIスレッドが音声系スレッドの終了を同期待ちしないことが前提
                //   （TimelineAudioPlayerのStopオフロード・Vst3EditorAudioFeederのJoin廃止）
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
                    dispatcher.Invoke(CreatePlugin);
                else
                    CreatePlugin();
            }
            catch (Exception e)
            {
                // プラグイン未インストール環境などでは素通しにする
                isLoadFailed = true;
                Log.Default.Write($"VST3プラグインの読み込みに失敗しました。path={item.PluginPath} classId={item.ClassId}", e);
            }
        }

        void CreatePlugin()
        {
            using var module = Vst3Module.Open(item.PluginPath);
            var newPlugin = module.CreatePlugin(item.ClassId);
            try
            {
                newPlugin.SetState(
                    hasPendingState ? pendingComponentState : item.ComponentState,
                    hasPendingState ? pendingControllerState : item.ControllerState);
                newPlugin.Setup(Input!.Hz, MaxBlockFrames);
                parameterSubscription.ReplayLatest();
                parameterSubscription.ApplyTo(newPlugin);
            }
            catch
            {
                newPlugin.Dispose();
                throw;
            }
            plugin = newPlugin;
            latencyFrames = Math.Max(0, plugin.GetLatencySamples());
            pendingComponentState = null;
            pendingControllerState = null;
            hasPendingState = false;
            disposer.Collect(plugin);
        }

        void HandleRestartFlags(long nextPosition)
        {
            if (plugin is null)
                return;
            var flags = plugin.ConsumeRestartFlags();
            if (!RequiresReload(flags))
                return;

            try
            {
                // 状態取得も制御系呼び出しのため、VST3の規約どおりUIスレッドで実行する
                byte[]? componentState = null;
                byte[]? controllerState = null;
                var savingPlugin = plugin;
                Vst3Plugin.InvokeOnUiThread(() =>
                {
                    if (!savingPlugin.IsDisposed)
                        (componentState, controllerState) = savingPlugin.GetState();
                });
                pendingComponentState = componentState;
                pendingControllerState = controllerState;
                hasPendingState = true;
            }
            catch (Exception e)
            {
                Log.Default.Write($"VST3プラグインの再初期化前の状態取得に失敗しました。path={item.PluginPath}", e);
                pendingComponentState = item.ComponentState;
                pendingControllerState = item.ControllerState;
                hasPendingState = true;
            }

            disposer.RemoveAndDispose(ref plugin);
            isLoadFailed = false;
            isPrimed = false;
            latencyFrames = 0;
            dryDelayBuffer = [];
            dryDelayIndex = 0;
            Input!.Seek(nextPosition);
            meterPublisher.Reset(nextPosition / 2, Hz);
        }

        internal static bool RequiresReload(int flags) =>
            (flags & (Vst3Native.RestartReloadComponent
                | Vst3Native.RestartIoChanged
                | Vst3Native.RestartLatencyChanged)) != 0;
    }
}
