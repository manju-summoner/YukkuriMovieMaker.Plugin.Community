using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class RecordingService
    {
        private readonly object syncRoot = new();
        private readonly RecordPathService recordPathService;

        private WaveInEvent? waveIn;
        private WaveFileWriter? writer;
        private string? currentFilePath;
        private DateTime recordingStartedAt;
        private long recordedBytes;
        private Exception? recordingStopException;
        private TaskCompletionSource<bool>? recordingStoppedTcs;

        public RecordingService(RecordPathService recordPathService)
        {
            this.recordPathService = recordPathService;
        }

        public bool IsRecording { get; private set; }

        public event EventHandler<RecordingDataEventArgs>? DataAvailable;
        public event EventHandler? RecordingStateChanged;

        public IReadOnlyList<string> GetAvailableDeviceNames()
        {
            var devices = new List<string>();
            for (var index = 0; index < WaveInEvent.DeviceCount; index++)
            {
                devices.Add(WaveInEvent.GetCapabilities(index).ProductName);
            }
            return devices;
        }

        public void StartRecording(string deviceName)
        {
            lock (syncRoot)
            {
                if (IsRecording)
                    return;

                var deviceIndex = FindDeviceIndex(deviceName);
                if (deviceIndex < 0)
                {
                    throw new InvalidOperationException($"録音デバイスを取得できません: {deviceName}");
                }

                var filePath = recordPathService.CreateRecordFilePath();

                try
                {
                    var input = new WaveInEvent
                    {
                        DeviceNumber = deviceIndex,
                        WaveFormat = new WaveFormat(RecordingAudioFormat.SampleRate, RecordingAudioFormat.BitDepth, RecordingAudioFormat.Channels),
                        BufferMilliseconds = 100
                    };
                    var output = new WaveFileWriter(filePath, input.WaveFormat);

                    input.DataAvailable += OnDataAvailable;
                    input.RecordingStopped += OnRecordingStopped;

                    waveIn = input;
                    writer = output;
                    currentFilePath = filePath;
                    recordingStartedAt = DateTime.Now;
                    recordedBytes = 0;
                    recordingStopException = null;
                    recordingStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    IsRecording = true;
                    OnRecordingStateChanged();

                    input.StartRecording();
                }
                catch
                {
                    CleanupRecordingResources(deleteFile: true);
                    throw;
                }
            }
        }

        public async Task<RecordedFileInfo?> StopRecordingAsync()
        {
            string? filePath;
            DateTime startedAt;
            TaskCompletionSource<bool>? stopCompletion;

            lock (syncRoot)
            {
                if (!IsRecording)
                    return null;

                try
                {
                    filePath = currentFilePath;
                    startedAt = recordingStartedAt;
                    stopCompletion = recordingStoppedTcs ?? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    recordingStoppedTcs = stopCompletion;
                    waveIn?.StopRecording();
                }
                catch
                {
                    CleanupRecordingResources(deleteFile: false);
                    throw;
                }
            }

            if (stopCompletion is null)
                throw new InvalidOperationException("録音停止の完了待機オブジェクトを取得できませんでした。");

            if (!await WaitForStopAsync(stopCompletion.Task, TimeSpan.FromSeconds(3)).ConfigureAwait(false))
                throw new TimeoutException("録音停止の完了待機がタイムアウトしました。");

            long dataLength;
            lock (syncRoot)
            {
                if (recordingStopException is not null)
                    throw new InvalidOperationException("録音停止に失敗しました。", recordingStopException);
                dataLength = recordedBytes;
            }

            var info = CreateRecordedFileInfo(filePath, startedAt, dataLength);
            return info;
        }

        public RecordedFileInfo? StopRecording()
        {
            return StopRecordingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private int FindDeviceIndex(string deviceName)
        {
            for (var index = 0; index < WaveInEvent.DeviceCount; index++)
            {
                if (string.Equals(WaveInEvent.GetCapabilities(index).ProductName, deviceName, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (syncRoot)
            {
                if (writer is null)
                    return;

                writer.Write(e.Buffer, 0, e.BytesRecorded);
                writer.Flush();
                recordedBytes += e.BytesRecorded;

                var volume = CalculateVolume(e.Buffer, e.BytesRecorded);
                DataAvailable?.Invoke(this, new RecordingDataEventArgs(volume));
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            TaskCompletionSource<bool>? stopCompletion = null;
            try
            {
                lock (syncRoot)
                {
                    recordingStopException = e.Exception;
                    stopCompletion = recordingStoppedTcs;
                    recordingStoppedTcs = null;
                    CleanupRecordingResources(deleteFile: false);
                }
            }
            finally
            {
                stopCompletion?.TrySetResult(true);
            }
        }

        private static async Task<bool> WaitForStopAsync(Task stopTask, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(stopTask, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != stopTask)
                return false;

            await stopTask.ConfigureAwait(false);
            return true;
        }

        private static double CalculateVolume(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded <= 0)
                return 0;

            double sum = 0;
            var sampleCount = bytesRecorded / 2;

            for (var index = 0; index + 2 <= bytesRecorded; index += 2)
            {
                var sample = BitConverter.ToInt16(buffer, index);
                var normalized = sample / 32768.0;
                sum += normalized * normalized;
            }

            return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
        }

        private static RecordedFileInfo? CreateRecordedFileInfo(string? filePath, DateTime startedAt, long dataLength)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            if (!File.Exists(filePath))
                return null;

            return new RecordedFileInfo
            {
                FilePath = filePath,
                Duration = DateTime.Now - startedAt,
                SampleRate = RecordingAudioFormat.SampleRate,
                Channels = RecordingAudioFormat.Channels,
                CreatedAt = DateTime.Now,
                DataLength = dataLength
            };
        }

        private void CleanupRecordingResources(bool deleteFile)
        {
            if (waveIn is not null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.RecordingStopped -= OnRecordingStopped;
                waveIn.Dispose();
                waveIn = null;
            }

            if (writer is not null)
            {
                writer.Dispose();
                writer = null;
            }

            var filePath = currentFilePath;
            currentFilePath = null;
            IsRecording = false;
            OnRecordingStateChanged();

            if (deleteFile && !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private void OnRecordingStateChanged()
        {
            RecordingStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}



