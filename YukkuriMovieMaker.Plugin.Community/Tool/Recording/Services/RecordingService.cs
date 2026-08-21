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

        private WasapiRecorder? waveIn;
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

                WasapiRecorder? input = null;
                WaveFileWriter? output = null;
                var transferred = false;

                try
                {
                    // 旧WasapiCapture(device)と同じIAudioClient::Initialize設定：
                    // 共有モード・ポーリング同期・バッファ100ms。
                    // ビルダーの既定値に依存するとNAudio側の既定変更で無言にレイテンシ特性が変わるため、
                    // 既定と同じ値でもすべて明示する（既定はイベント同期なのでWithPollingSync()は必須）。
                    // なおポーリング間隔だけはNAudio側の仕様が変わっており、旧実装が実確保バッファから
                    // 算出していたのに対し、WasapiRecorderはバッファ長の半分の固定値を使う。
                    input = new WasapiRecorderBuilder()
                        .WithDevice(targetDevice)
                        .WithSharedMode()
                        .WithPollingSync()
                        .WithBufferLength(100)
                        .Build();
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

                    // 既知の挙動差：旧WasapiCaptureはキャプチャスレッド開始直後に
                    // 「Startingのままなら Capturing にする」というガードを持っていて、開始直後の停止要求が
                    // 失われないようにしていたが、NAudio 3のWasapiRecorderは無条件にCapturingへ書き換える。
                    // このためStartRecording()の直後（キャプチャスレッドがCapturingを書くまでの数ms）に
                    // StopRecording()が入ると停止要求が上書きされて失われ、StopRecordingAsyncが
                    // 3秒待ってTimeoutExceptionを投げる。その後のCleanupRecordingResourcesで
                    // Dispose()→StopRecording()が再度走るため復旧はする（ハングはしない）。
                    input.StartRecording();
                    return new RecordingStartDeviceSelection(
                        targetDevice.ID,
                        targetDevice.FriendlyName,
                        fallbackToDefault);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RecordingService] StartRecording failed: {ex}");
                    CleanupRecordingResources(deleteFile: true);
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
            WasapiRecorder? captureInstance;

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

        /// <summary>
        /// WasapiRecorderのゼロコピーコールバック。bufferはWASAPIのバッファを直接指すため、
        /// このメソッドを抜けた後は無効になる（保持・非同期処理は不可）。
        /// 無音パケットはNAudio側がゼロ埋め済みのバッファを渡してくるので、こちらでの対処は不要。
        /// </summary>
        private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
        {
            lock (syncRoot)
            {
                if (writer is null || currentWaveFormat is null)
                    return;

                writer.Write(buffer);
                writer.Flush();
                recordedBytes += buffer.Length;

                var volume = CalculateVolume(buffer, currentWaveFormat);
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

        internal static double CalculateVolume(ReadOnlySpan<byte> buffer, WaveFormat format)
        {
            var bytesRecorded = buffer.Length;
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
                    var sample = BitConverter.ToSingle(buffer.Slice(index, 4));
                    sum += sample * sample;
                }
                return sampleCount == 0 ? 0 : Math.Sqrt(sum / sampleCount);
            }

            if (format.BitsPerSample == 16)
            {
                var sampleCount = bytesRecorded / 2;
                for (var index = 0; index + 2 <= bytesRecorded; index += 2)
                {
                    var sample = BitConverter.ToInt16(buffer.Slice(index, 2));
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
                    var sample = BitConverter.ToInt32(buffer.Slice(index, 4));
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
                // WasapiRecorder.Dispose()はキャプチャスレッドをJoinする。このメソッドはsyncRootを
                // 保持したまま、OnRecordingStopped（＝RecordingStoppedハンドラ）からも呼ばれるため、
                // ハンドラがどのスレッドで走るかに依存する。
                //
                // 不変条件：WasapiRecorderBuilder.Build()をUIスレッドで行うこと。
                // WasapiRecorderがSynchronizationContextを捕捉するのは（StartRecording()ではなく）
                // コンストラクタ＝Build()の時点で、RecordingStoppedはそのcontextへPostされる。
                // Build()をUIスレッドで行っていれば、Joinは終了間際のキャプチャスレッドをUIスレッドから
                // 待つだけなので即座に返る。
                //
                // SynchronizationContextの無いスレッドでBuild()すると、ハンドラがキャプチャスレッド上で
                // 同期実行され、次の2つが起きる：
                //   (1) このJoinが自スレッドJoinになって恒久ハングする
                //   (2) StopRecordingAsyncのタイムアウト経路（スレッドプール上でsyncRootを保持したまま
                //       Dispose()→Join()する）と、ハンドラ側のlock(syncRoot)が相互デッドロックする
                // いずれもsyncRootを握ったままなので、以後RecordingServiceの全メソッドが固まる。
                //
                // （旧WasapiCaptureはRecordingStopped発火の直前にcaptureThreadをnull化しており、
                // 　この経路のJoinは常にno-opだった。NAudio 3のWasapiRecorderはnull化しない）
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
    }

    public readonly record struct RecordingStartDeviceSelection(
        string DeviceId,
        string FriendlyName,
        bool FellBackToDefault)
    {
        public static RecordingStartDeviceSelection Empty { get; } = new(string.Empty, string.Empty, false);
    }
}



