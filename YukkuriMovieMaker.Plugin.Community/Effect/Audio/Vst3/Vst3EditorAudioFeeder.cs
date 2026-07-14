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

        // IEditorInfoは生成時点の値を固定したスナップショットのため、再生位置へ追従できるよう常に最新を取得する
        readonly Func<IEditorInfo?> getEditorInfo;
        readonly Vst3AudioEffect effect;
        readonly int hz;
        IAudioStream? source;
        bool isSourceUnavailable;
        long nextPosition = -1;
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

            var target = (long)(editorInfo.ItemPosition.Time.TotalSeconds * hz) * 2;
            target = Math.Clamp(target - target % 2, 0, stream.Duration);
            if (target == nextPosition)
                return false;

            // シーク（後退・大きなジャンプ）時は直前の1チャンク分から処理し直して表示を追従させる
            if (nextPosition < 0 || target < nextPosition || target - nextPosition > MaxChunkFrames * 2L)
            {
                nextPosition = Math.Max(0, target - MaxChunkFrames * 2L);
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
                if (stream.Position != nextPosition)
                    stream.Seek(nextPosition);
                if (readBuffer.Length < count)
                    readBuffer = new float[count];
                var read = stream.Read(readBuffer, 0, count);
                read -= read % 2;
                if (read <= 0)
                {
                    nextPosition = target;
                    return false;
                }

                var frames = read / 2;
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
                nextPosition += read;
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
