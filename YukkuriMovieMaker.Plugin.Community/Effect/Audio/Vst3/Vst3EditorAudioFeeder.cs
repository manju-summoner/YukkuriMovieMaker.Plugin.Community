using System;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// タイムラインの再生位置に追従して、エフェクトへの入力音声（UpTo選択）をエディター用プラグインへ処理させる。
    /// プラグインGUIの波形・アナライザー等が再生・シークにあわせてリアルタイムに更新されるようにする。
    /// 位置の基準はメーター転送と同じくアイテム相対の再生位置（IEditorInfo.ItemPosition）のため、
    /// 実際に聴こえている音と表示が同期する
    /// </summary>
    internal sealed class Vst3EditorAudioFeeder : IDisposable
    {
        /// <summary>
        /// 1回のFeedで処理する最大フレーム数。シーク時はこの分だけ手前から処理して表示を追従させ、
        /// 再生がこれ以上遅れた場合は読み飛ばして実時間へ追いつく
        /// </summary>
        const int MaxChunkFrames = 8192;

        /// <summary>
        /// UIスレッドで実行するため、1回のFeedに使ってよい処理時間。
        /// 超過が続く場合は重いエフェクト構成とみなしてフィードを停止する（表示は従来どおり凍結）
        /// </summary>
        const double MaxFeedMilliseconds = 8;
        const int MaxOverBudgetCount = 10;

        // IEditorInfoは生成時点の値を固定したスナップショットのため、再生位置へ追従できるよう常に最新を取得する
        readonly Func<IEditorInfo?> getEditorInfo;
        readonly Vst3AudioEffect effect;
        readonly int hz;
        IAudioStream? source;
        bool isSourceUnavailable;
        long nextPosition = -1;
        long lastDisplayTarget = -1;
        long latencyLeadSamples;
        int overBudgetCount;
        float[] readBuffer = [];
        float[] bufferL = [];
        float[] bufferR = [];

        public Vst3EditorAudioFeeder(Func<IEditorInfo?> getEditorInfo, Vst3AudioEffect effect, int hz)
        {
            this.getEditorInfo = getEditorInfo;
            this.effect = effect;
            this.hz = hz;
        }

        /// <summary>
        /// 前回から現在のタイムライン位置までの入力音声をプラグインへ処理させる。
        /// 位置が動いていない場合は何もせずfalseを返す
        /// </summary>
        public bool Feed(Vst3Plugin plugin)
        {
            var editorInfo = getEditorInfo();
            if (editorInfo is null)
                return false;
            var stream = EnsureSource(editorInfo);
            if (stream is null)
                return false;

            var displayTarget = (long)(editorInfo.ItemPosition.Time.TotalSeconds * hz) * 2;
            displayTarget = Math.Clamp(displayTarget - displayTarget % 2, 0, stream.Duration);
            // 実再生と同様に、申告レイテンシ分だけ入力を先行させて表示位置と実際に聴こえる音を一致させる。
            // 終端付近では超過分を無音として処理し、遅延出力を吐き切る（論理位置は終端を超えられる）
            var latencySamples = Math.Max(0, plugin.GetLatencySamples()) * 2L;
            var target = displayTarget + latencySamples;
            target -= target % 2;
            if (target == nextPosition && latencySamples == latencyLeadSamples)
                return false;

            var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            // シーク（後退・大きなジャンプ）と申告レイテンシの変更（kLatencyChanged）は、
            // 内部状態をリセットして直前から処理し直す。
            // 判定はフィード位置との距離ではなく表示位置の変化で行う。距離基準にすると、
            // 申告レイテンシが1チャンクを超えるプラグインでプライム中の残量が毎ティック
            // ジャンプ扱いになり、リセットと巻き戻しを繰り返して補正位置へ到達できない
            var isDiscontinuous = nextPosition < 0
                || latencySamples != latencyLeadSamples
                || displayTarget < lastDisplayTarget
                || displayTarget - lastDisplayTarget > MaxChunkFrames * 2L
                // フィードが実時間に追いつけず遅れが積み上がった場合は読み飛ばして追いつく
                || target - nextPosition > latencySamples + MaxChunkFrames * 2L * 2;
            lastDisplayTarget = displayTarget;
            if (isDiscontinuous)
            {
                latencyLeadSamples = latencySamples;
                // 非連続な音声を投入する前に内部状態（ディレイライン等）をリセットし、移動前の音声の混入を防ぐ
                plugin.Reset();
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

            try
            {
                if (readBuffer.Length < count)
                    readBuffer = new float[count];
                var available = (int)Math.Clamp(stream.Duration - nextPosition, 0, count);
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
                var succeeded = plugin.Process(
                    bufferL, bufferR, bufferL, bufferR,
                    frames,
                    nextPosition / 2,
                    new Vst3Transport(effect.Tempo, effect.TimeSignatureNumerator, effect.TimeSignatureDenominator, effect.IsTempoSyncEnabled));
                nextPosition += count;

                // UIスレッドを継続的にブロックしないよう、処理時間の超過が続いたらフィードを停止する
                var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
                if (elapsed.TotalMilliseconds > MaxFeedMilliseconds)
                {
                    if (++overBudgetCount >= MaxOverBudgetCount)
                    {
                        Log.Default.Write($"音声処理が重いため、VST3エディターへの音声転送を停止します。elapsed={elapsed.TotalMilliseconds:F1}ms");
                        Dispose();
                    }
                }
                else
                {
                    overBudgetCount = 0;
                }
                return succeeded;
            }
            catch (Exception e)
            {
                // アイテムの状態変化等で読み取りに失敗したら、以後はPumpのみで動作する
                Log.Default.Write("VST3エディターへの音声転送に失敗しました。", e);
                isSourceUnavailable = true;
                source?.Dispose();
                source = null;
                return false;
            }
        }

        IAudioStream? EnsureSource(IEditorInfo editorInfo)
        {
            if (source is not null || isSourceUnavailable)
                return source;
            try
            {
                source = editorInfo.CreateItemAudioSource(new ItemAudioSourceCreationParameter(AudioEffectSelection.UpTo(effect)));
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3エディター用の音声ソース作成に失敗しました。", e);
            }
            // 音声アイテム以外（null）や作成失敗、サンプルレート不一致の場合は以後試さない
            if (source is null || source.Hz != hz)
            {
                source?.Dispose();
                source = null;
                isSourceUnavailable = true;
            }
            return source;
        }

        public void Dispose()
        {
            source?.Dispose();
            source = null;
            isSourceUnavailable = true;
        }
    }
}
