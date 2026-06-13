using System;
using NAudio.Wave;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public sealed class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent? playbackOutput;
        private AudioFileReader? playbackReader;

        public bool IsPlaying { get; private set; }

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public void Play(string filePath)
        {
            Stop();

            playbackReader = new AudioFileReader(filePath);
            playbackOutput = new WaveOutEvent();
            playbackOutput.Volume = GetYmmVolume();
            playbackOutput.PlaybackStopped += OnPlaybackStopped;
            playbackOutput.Init(playbackReader);
            playbackOutput.Play();
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
                if (playbackOutput is not null)
                {
                    playbackOutput.PlaybackStopped -= OnPlaybackStopped;
                    if (!skipStopCall && playbackOutput.PlaybackState == PlaybackState.Playing)
                        playbackOutput.Stop();
                    playbackOutput.Dispose();
                    playbackOutput = null;
                }

                playbackReader?.Dispose();
                playbackReader = null;
            }
            finally
            {
                IsPlaying = false;
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
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
