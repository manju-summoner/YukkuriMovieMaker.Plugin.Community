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
    internal class Vst3AudioEffectProcessor(Vst3AudioEffect item) : AudioEffectProcessorBase
    {
        const int MaxBlockFrames = 4096;

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
        long position;

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
                var totalFrames = Duration / 2;
                var startFrame = position / 2;
                var endFrame = startFrame + Math.Max(0, frames - 1);
                var mixStart = (float)(item.Mix.GetValue(startFrame, totalFrames, Hz) * 0.01);
                var mixEnd = (float)(item.Mix.GetValue(endFrame, totalFrames, Hz) * 0.01);
                var mixStep = frames > 1 ? (mixEnd - mixStart) / (frames - 1) : 0;
                for (var i = 0; i < frames; i++)
                {
                    var mix = mixStart + mixStep * i;
                    destBuffer[offset + i * 2] = dryBuffer[i * 2] * (1 - mix) + bufferL[i] * mix;
                    destBuffer[offset + i * 2 + 1] = dryBuffer[i * 2 + 1] * (1 - mix) + bufferR[i] * mix;
                }
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
            plugin?.Reset();
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
                plugin.Process(bufferL, bufferR, bufferL, bufferR, frames, (position + processedFrames * 2L) / 2, CreateTransport());
                processedFrames += frames;
            }
        }

        Vst3Transport CreateTransport() => new(
            item.Tempo,
            item.TimeSignatureNumerator,
            item.TimeSignatureDenominator,
            item.IsTempoSyncEnabled);

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
                using var module = Vst3Module.Open(item.PluginPath);
                var newPlugin = module.CreatePlugin(item.ClassId);
                try
                {
                    newPlugin.SetState(item.ComponentState, item.ControllerState);
                    newPlugin.Setup(Input!.Hz, MaxBlockFrames);
                }
                catch
                {
                    newPlugin.Dispose();
                    throw;
                }
                plugin = newPlugin;
                latencyFrames = Math.Max(0, plugin.GetLatencySamples());
                disposer.Collect(plugin);
            }
            catch (Exception e)
            {
                // プラグイン未インストール環境などでは素通しにする
                isLoadFailed = true;
                Log.Default.Write($"VST3プラグインの読み込みに失敗しました。path={item.PluginPath} classId={item.ClassId}", e);
            }
        }
    }
}
