using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services
{
    public class RecordingService(RecordPathService recordPathService) : IDisposable
    {
        public const string DefaultRecordingDeviceId = "default";

        private readonly Lock syncRoot = new();
        private readonly RecordPathService recordPathService = recordPathService;

        private IWaveIn? waveIn;
        private WaveFileWriter? writer;
        private string? currentFilePath;
        private WaveFormat? currentWaveFormat;
        private MMDevice? currentDevice;
        private DateTime recordingStartedAt;
        private long recordedBytes;
        private Exception? recordingStopException;
        private TaskCompletionSource<bool>? recordingStoppedTcs;
        private bool disposed;

        public bool IsRecording { get; private set; }

        public event EventHandler<RecordingDataEventArgs>? DataAvailable;
        public event EventHandler? RecordingStateChanged;

        public IReadOnlyList<RecordingDeviceInfo> GetAvailableDevices()
        {
            var devices = new List<RecordingDeviceInfo>();
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    devices.Add(new RecordingDeviceInfo
                    {
                        Id = device.ID,
                        FriendlyName = device.FriendlyName,
                    });
                }
            }
            return devices;
        }

        public string? GetDefaultRecordingDeviceFriendlyName()
        {
            using var enumerator = new MMDeviceEnumerator();
            try
            {
                using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return device.FriendlyName;
            }
            catch
            {
                return null;
            }
        }

        public RecordingStartDeviceSelection StartRecording(string? deviceId)
        {
            lock (syncRoot)
            {
                if (IsRecording)
                    return RecordingStartDeviceSelection.Empty;

                MMDevice? targetDevice = null;
                var fallbackToDefault = false;
                var requestDefault = string.IsNullOrWhiteSpace(deviceId) || string.Equals(deviceId, DefaultRecordingDeviceId, StringComparison.Ordinal);

                using (var enumerator = new MMDeviceEnumerator())
                {
                    if (!requestDefault)
                    {
                        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                        {
                            if (targetDevice is null && string.Equals(device.ID, deviceId, StringComparison.Ordinal))
                            {
                                targetDevice = device;
                            }
                            else
                            {
                                device.Dispose();
                            }
                        }
                    }

                    if (targetDevice is null)
                    {
                        fallbackToDefault = !requestDefault;
                        try
                        {
                            targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                        }
                        catch
                        {
                            targetDevice = null;
                        }
                    }
                }

                if (targetDevice is null)
                    throw new InvalidOperationException(Texts.NoRecordingDeviceFoundDetailed);

                var filePath = recordPathService.CreateRecordFilePath();

                IWaveIn? input = null;
                WaveFileWriter? output = null;
                var transferred = false;

                try
                {
                    try
                    {
                        input = new WasapiCapture(targetDevice)
                        {
                            ShareMode = AudioClientShareMode.Shared
                        };
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new MicrophoneAccessDeniedException(ex);
                    }

                    output = new WaveFileWriter(filePath, input.WaveFormat);

                    input.DataAvailable += OnDataAvailable;
                    input.RecordingStopped += OnRecordingStopped;

                    waveIn = input;
                    writer = output;
                    currentFilePath = filePath;
                    currentWaveFormat = input.WaveFormat;
                    currentDevice = targetDevice;
                    recordingStartedAt = DateTime.Now;
                    recordedBytes = 0;
                    recordingStopException = null;
                    recordingStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    IsRecording = true;
                    transferred = true;
                    OnRecordingStateChanged();

                    input.StartRecording();
                    return new RecordingStartDeviceSelection(
                        targetDevice.ID,
                        targetDevice.FriendlyName,
                        fallbackToDefault);
                }
                catch (MicrophoneAccessDeniedException)
                {
                    CleanupRecordingResources(deleteFile: true);
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RecordingService] StartRecording failed: {ex}");
                    CleanupRecordingResources(deleteFile: true);
                    if (IsMicrophoneAccessDenied(ex))
                        throw new MicrophoneAccessDeniedException(ex);
                    throw;
                }
                finally
                {
                    if (!transferred && input is not null)
                    {
                        input.DataAvailable -= OnDataAvailable;
                        input.RecordingStopped -= OnRecordingStopped;
                        input.Dispose();
                    }

                    if (!transferred)
                    {
                        output?.Dispose();
                        targetDevice.Dispose();
                    }
                }
            }
        }

        public async Task<RecordedFileInfo?> StopRecordingAsync()
        {
            string? filePath;
            DateTime startedAt;
            TaskCompletionSource<bool>? stopCompletion;
            WaveFormat? format;
            IWaveIn? captureInstance;

            lock (syncRoot)
            {
                if (!IsRecording)
                    return null;

                filePath = currentFilePath;
                startedAt = recordingStartedAt;
                format = currentWaveFormat;
                stopCompletion = recordingStoppedTcs ?? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                recordingStoppedTcs = stopCompletion;
                captureInstance = waveIn;
            }

            try
            {
                captureInstance?.StopRecording();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RecordingService] StopRecordingAsync failed before waiting stop event: {ex}");
                lock (syncRoot)
                {
                    recordingStoppedTcs = null;
                    CleanupRecordingResources(deleteFile: false);
                }
                throw;
            }

            if (stopCompletion is null)
                throw new InvalidOperationException(Texts.StopWaitObjectUnavailable);

            if (!await WaitForStopAsync(stopCompletion.Task, TimeSpan.FromSeconds(3)).ConfigureAwait(false))
            {
                lock (syncRoot)
                {
                    recordingStoppedTcs = null;
                    CleanupRecordingResources(deleteFile: false);
                }
                throw new TimeoutException(Texts.StopWaitTimeout);
            }

            long dataLength;
            lock (syncRoot)
            {
                if (recordingStopException is not null)
                    throw new InvalidOperationException(Texts.RecordingStopFailedMessage, recordingStopException);
                dataLength = recordedBytes;
            }

            if (format is null)
                return null;

            var info = CreateRecordedFileInfo(filePath, startedAt, dataLength, format);
            return info;
        }

        public RecordedFileInfo? StopRecording()
        {
            return StopRecordingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            lock (syncRoot)
            {
                if (writer is null || currentWaveFormat is null)
                    return;

                writer.Write(e.Buffer, 0, e.BytesRecorded);
                writer.Flush();
                recordedBytes += e.BytesRecorded;

                var volume = CalculateVolume(e.Buffer, e.BytesRecorded, currentWaveFormat);
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

        private static double CalculateVolume(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            if (bytesRecorded <= 0)
                return 0;

            bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;
            if (format.Encoding == WaveFormatEncoding.Extensible && format is WaveFormatExtensible ext)
            {
                isFloat = ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
            }

            double sum = 0;

            if (isFloat)
            {
                var sampleCount = bytesRecorded / 4;
                for (var index = 0; index + 4 <= bytesRecorded; index += 4)
                {
                    var sample = BitConverter.ToSingle(buffer, index);
                    sum += sample * sample;
                }
                return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
            }

            if (format.BitsPerSample == 16)
            {
                var sampleCount = bytesRecorded / 2;
                for (var index = 0; index + 2 <= bytesRecorded; index += 2)
                {
                    var sample = BitConverter.ToInt16(buffer, index);
                    var normalized = sample / 32768.0;
                    sum += normalized * normalized;
                }
                return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
            }

            if (format.BitsPerSample == 24)
            {
                var sampleCount = bytesRecorded / 3;
                for (var index = 0; index + 3 <= bytesRecorded; index += 3)
                {
                    var sample = buffer[index] | (buffer[index + 1] << 8) | ((sbyte)buffer[index + 2] << 16);
                    var normalized = sample / 8388608.0;
                    sum += normalized * normalized;
                }
                return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
            }

            if (format.BitsPerSample == 32)
            {
                var sampleCount = bytesRecorded / 4;
                for (var index = 0; index + 4 <= bytesRecorded; index += 4)
                {
                    var sample = BitConverter.ToInt32(buffer, index);
                    var normalized = sample / 2147483648.0;
                    sum += normalized * normalized;
                }
                return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
            }

            return 0;
        }

        private static RecordedFileInfo? CreateRecordedFileInfo(string? filePath, DateTime startedAt, long dataLength, WaveFormat format)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            if (!File.Exists(filePath))
                return null;

            return new RecordedFileInfo
            {
                FilePath = filePath,
                Duration = DateTime.Now - startedAt,
                SampleRate = format.SampleRate,
                Channels = format.Channels,
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

            writer?.Dispose();
            writer = null;

            var filePath = currentFilePath;
            currentFilePath = null;
            currentWaveFormat = null;
            IsRecording = false;
            currentDevice?.Dispose();
            currentDevice = null;

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

        public void Dispose()
        {
            TaskCompletionSource<bool>? pendingStop;
            lock (syncRoot)
            {
                if (disposed)
                    return;
                disposed = true;

                pendingStop = recordingStoppedTcs;
                recordingStoppedTcs = null;

                CleanupRecordingResources(deleteFile: false);
            }

            // StopRecordingAsync を待っている呼び出し元があれば抜けさせる。
            pendingStop?.TrySetResult(false);
        }

        private static bool IsMicrophoneAccessDenied(Exception ex)
        {
            const int E_ACCESSDENIED = unchecked((int)0x80070005);
            return ex is System.Runtime.InteropServices.COMException com && com.HResult == E_ACCESSDENIED;
        }
    }

    public readonly record struct RecordingStartDeviceSelection(
        string DeviceId,
        string FriendlyName,
        bool FellBackToDefault)
    {
        public static RecordingStartDeviceSelection Empty { get; } = new(string.Empty, string.Empty, false);
    }

    public class MicrophoneAccessDeniedException(Exception inner)
        : Exception(inner.Message, inner)
    {
    }
}



