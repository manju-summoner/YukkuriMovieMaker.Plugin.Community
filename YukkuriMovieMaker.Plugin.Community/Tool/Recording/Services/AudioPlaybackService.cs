using System;
using NAudio.Wave;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public sealed class AudioPlaybackService : IDisposable
    {
        private AudioPlayer? playbackPlayer;
        private AudioFileReader? playbackReader;

        public bool IsPlaying { get; private set; }

        public event EventHandler? PlaybackStopped;

        public void Play(string filePath)
        {
            Stop();

            try
            {
                playbackReader = new AudioFileReader(filePath);
                playbackPlayer = new AudioPlayer(playbackReader) { Volume = GetYmmVolume() };
                // StreamEndedはAudioPlayerを構築したスレッドのSynchronizationContextへPostされる。
                // contextが無いと再生スレッド上で同期実行され、経路によってはハンドラからのDispose()が
                // 自スレッドのJoinになりうるため、AudioPlayerの構築とPlay()はUIスレッドから行うこと。
                playbackPlayer.StreamEnded += OnStreamEnded;
                playbackPlayer.Play();
            }
            catch
            {
                // 途中で失敗しても、生成済みのAudioPlayer/AudioFileReaderを抱えたままにしない。
                // 後始末そのものが失敗しても、再生失敗の原因である元の例外を握り潰さない。
                try { Stop(); } catch { }
                throw;
            }
            IsPlaying = true;
        }

        static float GetYmmVolume()
        {
            var settings = YMMSettings.Default;
            if (settings.IsMuted)
                return 0f;
            return (float)Math.Clamp(settings.Volume / 100.0, 0.0, 1.0);
        }

        public void Stop(bool skipStopCall = false)
        {
            try
            {
                if (playbackPlayer is not null)
                {
                    // 購読解除はStop()/Dispose()より先に行うこと。
                    // AudioPlayerのisManualStoppingはStop()が返った時点でfalseに戻るが、
                    // StreamEndedはSynchronizationContextへPostされてUIスレッドに戻ってから走るため、
                    // 手動停止をこのフラグでは抑止できない。停止後にPlaybackStoppedが飛ぶのを防いでいるのは
                    // ここで先に解除していることによる。
                    playbackPlayer.StreamEnded -= OnStreamEnded;
                    // Dispose()は内部でStop()を呼ぶので、skipStopCallは冗長なStop()呼び出しを省くだけ。
                    if (!skipStopCall && playbackPlayer.IsPlaying)
                        playbackPlayer.Stop();
                    playbackPlayer.Dispose();
                    playbackPlayer = null;
                }

                playbackReader?.Dispose();
                playbackReader = null;
            }
            finally
            {
                IsPlaying = false;
            }
        }

        private void OnStreamEnded(object? sender, EventArgs e)
        {
            Stop(skipStopCall: true);
            PlaybackStopped?.Invoke(this, e);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
