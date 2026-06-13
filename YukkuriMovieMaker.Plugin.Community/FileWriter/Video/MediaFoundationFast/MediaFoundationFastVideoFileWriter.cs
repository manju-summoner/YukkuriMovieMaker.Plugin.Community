using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Channels;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Interop;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;
using YukkuriMovieMaker.Plugin.FileWriter;
using H264Level = YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models.H264Level;
using H264Profile = YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models.H264Profile;
using VideoBitRateControlMode = YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models.VideoBitRateControlMode;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast;

internal sealed class MediaFoundationFastVideoFileWriter : IVideoFileWriter2
{
    private enum StreamKind { Video, Audio }
    private abstract record RawItem(long Sequence, byte[] Buffer, int Length, long Time, long Duration);
    private sealed record RawVideo(long Sequence, byte[] Buffer, int Length, long Time, long Duration) : RawItem(Sequence, Buffer, Length, Time, Duration);
    private sealed record RawAudio(long Sequence, byte[] Buffer, int Length, long Time, long Duration) : RawItem(Sequence, Buffer, Length, Time, Duration);
    private sealed record RawTexture(long Sequence, nint Texture, long Time, long Duration) : RawItem(Sequence, [], 0, Time, Duration);
    private sealed record PendingSample(StreamKind Kind, IMFSample Sample);

    private static readonly PendingSample SkippedSample = new(StreamKind.Audio, null!);

    private readonly MediaFoundationFastWriterSettings _settings;
    private readonly Channel<RawItem> _rawChannel;
    private readonly ConcurrentDictionary<long, PendingSample> _completed = new();
    private readonly ConcurrentQueue<PendingSample> _emitQueue = new();
    private readonly SemaphoreSlim _completedSignal = new(0);
    private readonly TaskCompletionSource<nint> _deviceDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _writerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _pipelineThread;
    private readonly GCLatencyMode _previousLatencyMode;
    private readonly bool _useNv12;
    private uint _videoIndex;
    private uint _audioIndex;
    private long _sequence;
    private long _emittedSequence = -1;
    private long _videoFrameIndex;
    private long _audioSamplePosition;
    private bool _gpuModeDecided;
    private bool _gpuMode;
    private bool _readbackMode;
    private nint _cachedBitmapPointer;
    private nint _cachedSourceTexture;
    private nint _gpuDevice;
    private nint _gpuContext;
    private readonly int _texturePoolSize;
    private readonly ConcurrentBag<nint> _freeTextures = new();
    private readonly SemaphoreSlim _textureSlots;
    private readonly TextureRecycler _recycler;
    private D3D11Unsafe.Texture2DDesc _textureDesc;
    private bool _textureDescReady;
    private ExceptionDispatchInfo? _failure;
    private volatile bool _disposed;

    public VideoFileWriterSupportedStreams SupportedStreams => VideoFileWriterSupportedStreams.Audio | VideoFileWriterSupportedStreams.Video;

    public MediaFoundationFastVideoFileWriter(string path, MediaFoundationFastWriterSettings settings)
    {
        _settings = settings;
        _useNv12 = settings.Width % 2 == 0 && settings.Height % 2 == 0;

        long frameBytes = Math.Max((long)settings.Width * settings.Height * 4, 1);
        _texturePoolSize = (int)Math.Clamp(268_435_456 / frameBytes, 8, 32);
        int capacity = (int)Math.Clamp(536_870_912 / frameBytes, 4, 32);
        _textureSlots = new SemaphoreSlim(_texturePoolSize, _texturePoolSize);
        _recycler = new TextureRecycler();
        _rawChannel = Channel.CreateBounded<RawItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _pipelineThread = new Thread(() => RunPipeline(path))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "MediaFoundationFastWriter",
        };
        _pipelineThread.Start();

        _previousLatencyMode = GCSettings.LatencyMode;
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }

    public unsafe void WriteVideo(ID2D1Bitmap1 frame)
    {
        ThrowIfFailed();
        if (!_gpuModeDecided)
            DecideGpuMode(frame);

        if (_gpuMode)
        {
            WriteVideoGpu(frame);
            return;
        }
        if (_readbackMode)
        {
            WriteVideoReadback(frame);
            return;
        }

        var size = frame.PixelSize;
        int length = size.Width * size.Height * 4;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        var map = frame.Map(MapOptions.Read);
        try
        {
            int rowBytes = size.Width * 4;
            if (map.Pitch == rowBytes)
            {
                CopyFromPointer(map.Bits, buffer, length);
            }
            else
            {
                fixed (byte* dst = buffer)
                {
                    byte* src = (byte*)map.Bits;
                    byte* d = dst;
                    for (int y = 0; y < size.Height; y++)
                    {
                        Buffer.MemoryCopy(src, d, rowBytes, rowBytes);
                        src += map.Pitch;
                        d += rowBytes;
                    }
                }
            }
        }
        finally
        {
            frame.Unmap();
        }
        EnqueueVideo(buffer, length);
    }

    public void WriteVideo(byte[] frame)
    {
        ThrowIfFailed();
        if (!_gpuModeDecided)
        {
            _gpuModeDecided = true;
            _gpuMode = false;
            _deviceDecision.TrySetResult(0);
        }
        var buffer = ArrayPool<byte>.Shared.Rent(frame.Length);
        CopyFrame(frame, buffer);
        EnqueueVideo(buffer, frame.Length);
    }

    private void EnqueueVideo(byte[] buffer, int length)
    {
        long index = _videoFrameIndex++;
        long time = VideoFrameToTime(index);
        long duration = VideoFrameToTime(index + 1) - time;
        long sequence = NextSequence();
        try
        {
            Enqueue(new RawVideo(sequence, buffer, length, time, duration));
        }
        catch
        {
            MarkSkipped(sequence);
            throw;
        }
    }

    private void DecideGpuMode(ID2D1Bitmap1 frame)
    {
        _gpuModeDecided = true;
        nint surface = 0;
        nint texture = 0;
        try
        {
            using var dxgiSurface = frame.Surface;
            surface = dxgiSurface.NativePointer;
            Marshal.AddRef(surface);
            texture = D3D11Unsafe.QueryInterface(surface, MediaFoundationGuids.IID_ID3D11Texture2D);
            _gpuDevice = D3D11Unsafe.GetDevice(texture);
            _gpuContext = D3D11Unsafe.GetImmediateContext(_gpuDevice);
            if (D3D11Unsafe.SupportsVideo(_gpuDevice))
            {
                _gpuMode = true;
                _deviceDecision.TrySetResult(_gpuDevice);
            }
            else
            {
                _readbackMode = true;
                _deviceDecision.TrySetResult(0);
            }
        }
        catch
        {
            _gpuMode = false;
            _deviceDecision.TrySetResult(0);
        }
        finally
        {
            if (texture != 0) Marshal.Release(texture);
            if (surface != 0) Marshal.Release(surface);
        }
    }

    private nint ResolveSourceTexture(ID2D1Bitmap1 frame)
    {
        nint bitmapPointer = frame.NativePointer;
        if (bitmapPointer == _cachedBitmapPointer && _cachedSourceTexture != 0)
            return _cachedSourceTexture;

        if (_cachedSourceTexture != 0)
        {
            Marshal.Release(_cachedSourceTexture);
            _cachedSourceTexture = 0;
            _cachedBitmapPointer = 0;
        }
        using var dxgiSurface = frame.Surface;
        nint surface = dxgiSurface.NativePointer;
        _cachedSourceTexture = D3D11Unsafe.QueryInterface(surface, MediaFoundationGuids.IID_ID3D11Texture2D);
        _cachedBitmapPointer = bitmapPointer;
        return _cachedSourceTexture;
    }

    private void WriteVideoReadback(ID2D1Bitmap1 frame)
    {
        _writerReady.Task.GetAwaiter().GetResult();

        long index = _videoFrameIndex++;
        long sequence = NextSequence();
        try
        {
            WriteVideoReadbackCore(frame, index, sequence);
        }
        catch
        {
            MarkSkipped(sequence);
            throw;
        }
    }

    private void WriteVideoReadbackCore(ID2D1Bitmap1 frame, long index, long sequence)
    {
        while (!_textureSlots.Wait(100))
        {
            ThrowIfFailed();
        }

        nint staging = 0;
        bool handedOff = false;
        try
        {
            nint sourceTexture = ResolveSourceTexture(frame);

            if (!_textureDescReady)
            {
                var desc = D3D11Unsafe.GetTextureDesc(sourceTexture);
                desc.MipLevels = 1;
                desc.ArraySize = 1;
                desc.SampleCount = 1;
                desc.SampleQuality = 0;
                desc.Usage = 3;
                desc.BindFlags = 0;
                desc.CPUAccessFlags = 0x20000;
                desc.MiscFlags = 0;
                _textureDesc = desc;
                _textureDescReady = true;
            }
            if (!_freeTextures.TryTake(out staging))
                staging = D3D11Unsafe.CreateTexture2D(_gpuDevice, _textureDesc);

            D3D11Unsafe.CopyResource(_gpuContext, staging, sourceTexture);
            D3D11Unsafe.Flush(_gpuContext);

            long time = VideoFrameToTime(index);
            Enqueue(new RawTexture(sequence, staging, time, VideoFrameToTime(index + 1) - time));
            handedOff = true;
        }
        finally
        {
            if (!handedOff)
            {
                if (staging != 0) _freeTextures.Add(staging);
                _textureSlots.Release();
            }
        }
    }

    private void WriteVideoGpu(ID2D1Bitmap1 frame)
    {
        _writerReady.Task.GetAwaiter().GetResult();
        WriteVideoGpuCore(frame, _videoFrameIndex++);
    }

    private void WriteVideoGpuCore(ID2D1Bitmap1 frame, long index)
    {
        while (!_textureSlots.Wait(100))
        {
            ThrowIfFailed();
        }

        nint texture = 0;
        bool slotConsumed = false;
        try
        {
            nint sourceTexture = ResolveSourceTexture(frame);

            if (!_textureDescReady)
            {
                var desc = D3D11Unsafe.GetTextureDesc(sourceTexture);
                desc.MipLevels = 1;
                desc.ArraySize = 1;
                desc.SampleCount = 1;
                desc.SampleQuality = 0;
                desc.Usage = 0;
                desc.BindFlags = 0x28;
                desc.CPUAccessFlags = 0;
                desc.MiscFlags = 0;
                _textureDesc = desc;
                _textureDescReady = true;
            }
            if (!_freeTextures.TryTake(out texture))
                texture = D3D11Unsafe.CreateTexture2D(_gpuDevice, _textureDesc);

            D3D11Unsafe.CopyResource(_gpuContext, texture, sourceTexture);

            IMFSample? sample = null;
            IMFMediaBuffer? buffer = null;
            try
            {
                sample = MediaFoundationApi.CreateTrackedSample();
                nint capturedTexture = texture;
                ((IMFTrackedSample)sample).SetAllocator(_recycler, new RecycleToken(() =>
                {
                    _freeTextures.Add(capturedTexture);
                    _textureSlots.Release();
                }));
                slotConsumed = true;
                nint sampleTexture = texture;
                texture = 0;
                buffer = MediaFoundationApi.CreateDxgiSurfaceBuffer(sampleTexture);
                buffer.SetCurrentLength((uint)(_settings.Width * _settings.Height * 4));
                sample.AddBuffer(buffer);
                long time = VideoFrameToTime(index);
                sample.SetSampleTime(time);
                sample.SetSampleDuration(VideoFrameToTime(index + 1) - time);
                _emitQueue.Enqueue(new PendingSample(StreamKind.Video, sample));
                _completedSignal.Release();
            }
            catch
            {
                MediaFoundationApi.Release(sample);
                throw;
            }
            finally
            {
                MediaFoundationApi.Release(buffer);
            }
        }
        finally
        {
            if (texture != 0)
            {
                _freeTextures.Add(texture);
                if (!slotConsumed) _textureSlots.Release();
            }
            else if (!slotConsumed)
            {
                _textureSlots.Release();
            }
        }
    }

    [ComVisible(true)]
    private sealed class RecycleToken(Action onReleased)
    {
        public Action OnReleased { get; } = onReleased;
    }

    [ComVisible(true)]
    private sealed class TextureRecycler : IMFAsyncCallback
    {
        public int GetParameters(out uint pdwFlags, out uint pdwQueue)
        {
            pdwFlags = 0;
            pdwQueue = 0;
            return unchecked((int)0x80004001);
        }

        public int Invoke(IMFAsyncResult pAsyncResult)
        {
            try
            {
                pAsyncResult.GetState(out var state);
                if (state is RecycleToken token)
                    token.OnReleased();
            }
            catch
            {
            }
            return 0;
        }
    }

    public unsafe void WriteAudio(float[] samples)
    {
        ThrowIfFailed();
        if (samples is null || samples.Length == 0) return;

        var buffer = ArrayPool<byte>.Shared.Rent(samples.Length * 2);
        fixed (float* src = samples)
        fixed (byte* dst = buffer)
        {
            ConvertFloatToPcm16(src, (short*)dst, samples.Length);
        }
        long position = _audioSamplePosition;
        _audioSamplePosition += samples.Length;
        long time = AudioPositionToTime(position);
        long duration = AudioPositionToTime(position + samples.Length) - time;
        long sequence = _gpuMode ? 0 : NextSequence();
        try
        {
            Enqueue(new RawAudio(sequence, buffer, samples.Length * 2, time, duration));
        }
        catch when (!_gpuMode)
        {
            MarkSkipped(sequence);
            throw;
        }
    }

    private void Enqueue(RawItem item)
    {
        try
        {
            if (!_rawChannel.Writer.TryWrite(item))
                _rawChannel.Writer.WriteAsync(item).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            if (item.Buffer.Length > 0)
                ArrayPool<byte>.Shared.Return(item.Buffer);
            ThrowIfFailed();
            throw;
        }
    }

    private void RunPipeline(string path)
    {
        IMFSinkWriter? writer = null;
        HardwareDeviceContext? hardwareContext = null;
        bool started = false;
        try
        {
            nint externalDevice = _deviceDecision.Task.GetAwaiter().GetResult();
            try
            {
                MediaFoundationApi.Startup();
                started = true;
                if (externalDevice != 0)
                {
                    hardwareContext = MediaFoundationApi.CreateHardwareDeviceContextFromDevice(externalDevice);
                    (writer, _videoIndex, _audioIndex) = CreateWriter(path, _settings, hardwareContext, useNv12: false, disableThrottling: true);
                }
                else
                {
                    try
                    {
                        if (_settings.IsHardwareAcceleration)
                            hardwareContext = MediaFoundationApi.CreateHardwareDeviceContext();
                        (writer, _videoIndex, _audioIndex) = CreateWriter(path, _settings, hardwareContext, _useNv12, disableThrottling: true);
                    }
                    catch (Exception ex) when (hardwareContext is not null)
                    {
                        Log.Default.Write(Texts.HardwareFallback, ex);
                        hardwareContext.Dispose();
                        hardwareContext = null;
                        (writer, _videoIndex, _audioIndex) = CreateWriter(path, _settings, null, _useNv12, disableThrottling: true);
                    }
                }
                _writerReady.TrySetResult();
            }
            catch (Exception ex)
            {
                _failure ??= ExceptionDispatchInfo.Capture(ex);
                _writerReady.TrySetException(ex);
                _rawChannel.Writer.TryComplete();
                while (_rawChannel.Reader.TryRead(out var remaining))
                    ArrayPool<byte>.Shared.Return(remaining.Buffer);
                return;
            }

            try
            {
                int workerCount = (_gpuMode || _readbackMode) ? 1 : Math.Clamp(Environment.ProcessorCount / 4, 1, 4);
                var workers = new Task[workerCount];
                for (int i = 0; i < workerCount; i++)
                    workers[i] = Task.Run(ConvertWorkerAsync);
                var allWorkers = Task.WhenAll(workers);

                if (_gpuMode)
                {
                    long idleMilliseconds = 0;
                    while (true)
                    {
                        if (_emitQueue.TryDequeue(out var pending))
                        {
                            try
                            {
                                writer.WriteSample(pending.Kind == StreamKind.Video ? _videoIndex : _audioIndex, pending.Sample);
                            }
                            finally
                            {
                                MediaFoundationApi.Release(pending.Sample);
                            }
                            idleMilliseconds = 0;
                            continue;
                        }
                        if (_failure is not null)
                            ThrowIfFailed();
                        if (allWorkers.IsCompleted && _emitQueue.IsEmpty)
                            break;
                        if (_completedSignal.Wait(100))
                        {
                            idleMilliseconds = 0;
                        }
                        else
                        {
                            idleMilliseconds += 100;
                            if (idleMilliseconds >= 5000 && _disposed)
                                throw new TimeoutException("MediaFoundationFast pipeline stalled.");
                        }
                    }
                }
                else
                {
                    long next = 0;
                    long idleMilliseconds = 0;
                    while (true)
                    {
                        if (_completed.TryRemove(next, out var pending))
                        {
                            if (!ReferenceEquals(pending, SkippedSample))
                            {
                                try
                                {
                                    writer.WriteSample(pending.Kind == StreamKind.Video ? _videoIndex : _audioIndex, pending.Sample);
                                }
                                finally
                                {
                                    MediaFoundationApi.Release(pending.Sample);
                                }
                            }
                            Volatile.Write(ref _emittedSequence, next);
                            next++;
                            idleMilliseconds = 0;
                            continue;
                        }
                        if (_failure is not null)
                            ThrowIfFailed();
                        if (allWorkers.IsCompleted && _completed.IsEmpty && next >= Interlocked.Read(ref _sequence))
                            break;
                        if (_completedSignal.Wait(100))
                        {
                            idleMilliseconds = 0;
                        }
                        else
                        {
                            idleMilliseconds += 100;
                            if (idleMilliseconds >= 5000 && _disposed)
                                throw new TimeoutException("MediaFoundationFast pipeline stalled.");
                        }
                    }
                }
                allWorkers.GetAwaiter().GetResult();
                ThrowIfFailed();
                writer.DoFinalize();
            }
            catch (Exception ex)
            {
                _failure ??= ExceptionDispatchInfo.Capture(ex);
                _rawChannel.Writer.TryComplete();
                while (_rawChannel.Reader.TryRead(out var remaining))
                    ArrayPool<byte>.Shared.Return(remaining.Buffer);
                foreach (var key in _completed.Keys)
                {
                    if (_completed.TryRemove(key, out var pending) && !ReferenceEquals(pending, SkippedSample))
                        MediaFoundationApi.Release(pending.Sample);
                }
                while (_emitQueue.TryDequeue(out var queued))
                    MediaFoundationApi.Release(queued.Sample);
            }
        }
        finally
        {
            MediaFoundationApi.Release(writer);
            long drainDeadline = Environment.TickCount64 + 3000;
            for (int i = 0; i < _texturePoolSize; i++)
            {
                int remaining = (int)(drainDeadline - Environment.TickCount64);
                if (remaining <= 0 || !_textureSlots.Wait(remaining))
                    break;
            }
            while (_freeTextures.TryTake(out var pooled))
                Marshal.Release(pooled);
            if (_cachedSourceTexture != 0) { Marshal.Release(_cachedSourceTexture); _cachedSourceTexture = 0; }
            if (_gpuContext != 0) { Marshal.Release(_gpuContext); _gpuContext = 0; }
            if (_gpuDevice != 0) { Marshal.Release(_gpuDevice); _gpuDevice = 0; }
            hardwareContext?.Dispose();
            if (started)
                MediaFoundationApi.Shutdown();
        }
    }

    private async Task ConvertWorkerAsync()
    {
        try
        {
            await foreach (var item in _rawChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    var sample = item is RawTexture rawTexture ? CreateSampleFromStaging(rawTexture) : CreateSample(item);
                    var kind = item is RawAudio ? StreamKind.Audio : StreamKind.Video;
                    if (_gpuMode)
                        _emitQueue.Enqueue(new PendingSample(kind, sample));
                    else
                        _completed[item.Sequence] = new PendingSample(kind, sample);
                }
                catch when (!_gpuMode)
                {
                    _completed[item.Sequence] = SkippedSample;
                    throw;
                }
                finally
                {
                    if (item is RawTexture handed)
                    {
                        _freeTextures.Add(handed.Texture);
                        _textureSlots.Release();
                    }
                    else if (item.Buffer.Length > 0)
                    {
                        ArrayPool<byte>.Shared.Return(item.Buffer);
                    }
                    _completedSignal.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _failure ??= ExceptionDispatchInfo.Capture(ex);
            _rawChannel.Writer.TryComplete();
            while (_rawChannel.Reader.TryRead(out var remaining))
            {
                if (remaining is RawTexture handed)
                {
                    _freeTextures.Add(handed.Texture);
                    _textureSlots.Release();
                }
                else if (remaining.Buffer.Length > 0)
                {
                    ArrayPool<byte>.Shared.Return(remaining.Buffer);
                }
                if (!_gpuMode)
                    _completed[remaining.Sequence] = SkippedSample;
            }
        }
        finally
        {
            _completedSignal.Release();
        }
    }

    private unsafe IMFSample CreateSampleFromStaging(RawTexture item)
    {
        IMFSample? sample = null;
        IMFMediaBuffer? buffer = null;
        D3D11Unsafe.MappedSubresource mapped;
        int spins = 0;
        while (!D3D11Unsafe.TryMapNoWait(_gpuContext, item.Texture, out mapped))
        {
            ThrowIfFailed();
            if (++spins < 20)
                Thread.Yield();
            else
                Thread.Sleep(1);
        }
        try
        {
            int width = _settings.Width;
            int height = _settings.Height;
            int outputLength = _useNv12 ? width * height * 3 / 2 : width * height * 4;
            sample = MediaFoundationApi.CreateSample();
            buffer = MediaFoundationApi.CreateMemoryBuffer((uint)outputLength);
            buffer.Lock(out var ptr, out _, out _);
            try
            {
                if (_useNv12)
                {
                    ConvertBgraToNv12Pointer((byte*)mapped.Data, (int)mapped.RowPitch, ptr, width, height);
                }
                else
                {
                    byte* src = (byte*)mapped.Data;
                    byte* dst = (byte*)ptr;
                    int rowBytes = width * 4;
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(src, dst, rowBytes, rowBytes);
                        src += mapped.RowPitch;
                        dst += rowBytes;
                    }
                }
                buffer.SetCurrentLength((uint)outputLength);
            }
            finally
            {
                buffer.Unlock();
            }
            sample.AddBuffer(buffer);
            sample.SetSampleTime(item.Time);
            sample.SetSampleDuration(item.Duration);
            return sample;
        }
        catch
        {
            MediaFoundationApi.Release(sample);
            throw;
        }
        finally
        {
            MediaFoundationApi.Release(buffer);
            D3D11Unsafe.Unmap(_gpuContext, item.Texture);
        }
    }

    private unsafe IMFSample CreateSample(RawItem item)
    {
        IMFSample? sample = null;
        IMFMediaBuffer? buffer = null;
        try
        {
            bool nv12Video = item is RawVideo && _useNv12 && !_gpuMode;
            int outputLength = nv12Video ? _settings.Width * _settings.Height * 3 / 2 : item.Length;
            sample = MediaFoundationApi.CreateSample();
            buffer = MediaFoundationApi.CreateMemoryBuffer((uint)outputLength);
            buffer.Lock(out var ptr, out _, out _);
            try
            {
                if (nv12Video)
                {
                    ConvertBgraToNv12(item.Buffer, ptr, _settings.Width, _settings.Height);
                }
                else
                {
                    fixed (byte* src = item.Buffer)
                    {
                        Buffer.MemoryCopy(src, (void*)ptr, item.Length, item.Length);
                    }
                }
                buffer.SetCurrentLength((uint)outputLength);
            }
            finally
            {
                buffer.Unlock();
            }
            sample.AddBuffer(buffer);
            sample.SetSampleTime(item.Time);
            sample.SetSampleDuration(item.Duration);
            return sample;
        }
        catch
        {
            MediaFoundationApi.Release(sample);
            throw;
        }
        finally
        {
            MediaFoundationApi.Release(buffer);
        }
    }

    private static (IMFSinkWriter Writer, uint VideoIndex, uint AudioIndex) CreateWriter(string path, MediaFoundationFastWriterSettings settings, HardwareDeviceContext? hardwareContext, bool useNv12, bool disableThrottling)
    {
        IMFAttributes? attributes = null;
        IMFSinkWriter? writer = null;
        try
        {
            attributes = CreateSinkWriterAttributes(settings, hardwareContext, disableThrottling);
            writer = MediaFoundationApi.CreateSinkWriterFromURL(path, attributes);
            uint videoIndex = CreateVideoStream(settings, writer, useNv12);
            uint audioIndex = CreateAudioStream(settings, writer);
            writer.BeginWriting();
            return (writer, videoIndex, audioIndex);
        }
        catch
        {
            MediaFoundationApi.Release(writer);
            throw;
        }
        finally
        {
            MediaFoundationApi.Release(attributes);
        }
    }

    private static IMFAttributes CreateSinkWriterAttributes(MediaFoundationFastWriterSettings settings, HardwareDeviceContext? hardwareContext, bool disableThrottling)
    {
        var attributes = MediaFoundationApi.CreateAttributes(16);
        try
        {
            attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncCommonRateControlMode, (uint)settings.VideoBitRateControlMode);
            switch (settings.VideoBitRateControlMode)
            {
                case VideoBitRateControlMode.CBR:
                case VideoBitRateControlMode.UnconstrainedVBR:
                    attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncCommonMeanBitRate, (uint)(Math.Min(ResolveBitRateKbps(settings), 2097151) * 1024));
                    break;
                case VideoBitRateControlMode.Quality:
                    attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncCommonQuality, (uint)Math.Clamp(settings.VideoQuality, 1, 100));
                    break;
                default:
                    throw new NotSupportedException();
            }
            attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncCommonQualityVsSpeed, (uint)Math.Clamp(100 - settings.EncodeSpeed, 0, 100));
            attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncNumWorkerThreads, (uint)(settings.NumberOfThreads <= 0 ? Environment.ProcessorCount : settings.NumberOfThreads));
            if (settings.GOPSize > 0)
                attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncMPVGOPSize, (uint)settings.GOPSize);
            attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncMPVDefaultBPictureCount, (uint)(settings.H264Profile == H264Profile.Baseline ? 0 : Math.Max(settings.BFrameCount, 0)));
            attributes.SetUINT32(MediaFoundationGuids.CODECAPI_AVEncH264CABACEnable, settings.H264Profile == H264Profile.Baseline ? 0u : 1u);
            if (hardwareContext is not null)
            {
                attributes.SetUnknown(MediaFoundationGuids.MF_SINK_WRITER_D3D_MANAGER, hardwareContext.Manager);
                attributes.SetUINT32(MediaFoundationGuids.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);
            }
            attributes.SetUINT32(MediaFoundationGuids.MF_SINK_WRITER_DISABLE_THROTTLING, disableThrottling ? 1u : 0u);
            attributes.SetUINT32(MediaFoundationGuids.MF_LOW_LATENCY, 0);
            return attributes;
        }
        catch
        {
            MediaFoundationApi.Release(attributes);
            throw;
        }
    }

    private static int ResolveBitRateKbps(MediaFoundationFastWriterSettings settings)
    {
        if (settings.VideoBitRate > 0) return settings.VideoBitRate;
        long auto = (long)settings.Width * settings.Height * settings.FPS / 10000;
        return (int)Math.Clamp(auto, 1000, 200000);
    }

    private static uint CreateVideoStream(MediaFoundationFastWriterSettings settings, IMFSinkWriter writer, bool useNv12)
    {
        uint index;
        var outputType = MediaFoundationApi.CreateMediaType();
        try
        {
            outputType.SetGUID(MediaFoundationGuids.MF_MT_MAJOR_TYPE, MediaFoundationGuids.MFMediaType_Video);
            outputType.SetGUID(MediaFoundationGuids.MF_MT_SUBTYPE, MediaFoundationGuids.MFVideoFormat_H264);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_MPEG2_PROFILE, (uint)settings.H264Profile);
            if (settings.H264Level != H264Level.Auto)
                outputType.SetUINT32(MediaFoundationGuids.MF_MT_MPEG2_LEVEL, (uint)settings.H264Level);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_INTERLACE_MODE, 2);
            outputType.SetUINT64(MediaFoundationGuids.MF_MT_FRAME_SIZE, ((ulong)(uint)settings.Width << 32) | (uint)settings.Height);
            outputType.SetUINT64(MediaFoundationGuids.MF_MT_FRAME_RATE, ((ulong)(uint)settings.FPS << 32) | 1);
            outputType.SetUINT64(MediaFoundationGuids.MF_MT_PIXEL_ASPECT_RATIO, 4294967297UL);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_VIDEO_PRIMARIES, 2);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_TRANSFER_FUNCTION, 5);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_VIDEO_NOMINAL_RANGE, 2);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_YUV_MATRIX, 1);
            writer.AddStream(outputType, out index);
        }
        finally
        {
            MediaFoundationApi.Release(outputType);
        }

        var inputType = MediaFoundationApi.CreateMediaType();
        try
        {
            inputType.SetGUID(MediaFoundationGuids.MF_MT_MAJOR_TYPE, MediaFoundationGuids.MFMediaType_Video);
            inputType.SetUINT32(MediaFoundationGuids.MF_MT_INTERLACE_MODE, 2);
            inputType.SetUINT64(MediaFoundationGuids.MF_MT_FRAME_SIZE, ((ulong)(uint)settings.Width << 32) | (uint)settings.Height);
            inputType.SetUINT64(MediaFoundationGuids.MF_MT_FRAME_RATE, ((ulong)(uint)settings.FPS << 32) | 1);
            inputType.SetUINT64(MediaFoundationGuids.MF_MT_PIXEL_ASPECT_RATIO, 4294967297UL);
            inputType.SetUINT32(MediaFoundationGuids.MF_MT_VIDEO_PRIMARIES, 2);
            if (useNv12)
            {
                inputType.SetGUID(MediaFoundationGuids.MF_MT_SUBTYPE, MediaFoundationGuids.MFVideoFormat_NV12);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_DEFAULT_STRIDE, (uint)settings.Width);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_TRANSFER_FUNCTION, 5);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_VIDEO_NOMINAL_RANGE, 2);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_YUV_MATRIX, 1);
            }
            else
            {
                inputType.SetGUID(MediaFoundationGuids.MF_MT_SUBTYPE, MediaFoundationGuids.MFVideoFormat_RGB32);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_DEFAULT_STRIDE, (uint)(settings.Width * 4));
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_TRANSFER_FUNCTION, 8);
                inputType.SetUINT32(MediaFoundationGuids.MF_MT_VIDEO_NOMINAL_RANGE, 1);
            }
            writer.SetInputMediaType(index, inputType, null);
        }
        finally
        {
            MediaFoundationApi.Release(inputType);
        }
        return index;
    }

    private static uint CreateAudioStream(MediaFoundationFastWriterSettings settings, IMFSinkWriter writer)
    {
        uint audioBytesPerSecond = (uint)settings.AudioBitRate switch
        {
            >= 192 => 24000,
            >= 160 => 20000,
            >= 128 => 16000,
            _ => 12000,
        };
        uint index;
        var outputType = MediaFoundationApi.CreateMediaType();
        try
        {
            outputType.SetGUID(MediaFoundationGuids.MF_MT_MAJOR_TYPE, MediaFoundationGuids.MFMediaType_Audio);
            outputType.SetGUID(MediaFoundationGuids.MF_MT_SUBTYPE, MediaFoundationGuids.MFAudioFormat_AAC);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)settings.Hz);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_NUM_CHANNELS, 2);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, audioBytesPerSecond);
            outputType.SetUINT32(MediaFoundationGuids.MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, (uint)settings.AACProfile);
            writer.AddStream(outputType, out index);
        }
        finally
        {
            MediaFoundationApi.Release(outputType);
        }

        var inputType = MediaFoundationApi.CreateMediaType();
        try
        {
            inputType.SetGUID(MediaFoundationGuids.MF_MT_MAJOR_TYPE, MediaFoundationGuids.MFMediaType_Audio);
            inputType.SetGUID(MediaFoundationGuids.MF_MT_SUBTYPE, MediaFoundationGuids.MFAudioFormat_PCM);
            inputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)settings.Hz);
            inputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
            inputType.SetUINT32(MediaFoundationGuids.MF_MT_AUDIO_NUM_CHANNELS, 2);
            writer.SetInputMediaType(index, inputType, null);
        }
        finally
        {
            MediaFoundationApi.Release(inputType);
        }
        return index;
    }

    private static unsafe void ConvertBgraToNv12(byte[] frame, nint destination, int width, int height)
    {
        var handle = GCHandle.Alloc(frame, GCHandleType.Pinned);
        try
        {
            ConvertBgraToNv12Pointer((byte*)handle.AddrOfPinnedObject(), width * 4, destination, width, height);
        }
        finally
        {
            handle.Free();
        }
    }

    private static unsafe void ConvertBgraToNv12Pointer(byte* source, int sourcePitch, nint destination, int width, int height)
    {
        {
            byte* src = source;
            byte* yPlane = (byte*)destination;
            byte* uvPlane = yPlane + (long)width * height;

            Parallel.For(0, height / 2, pair =>
            {
                int top = pair * 2;
                byte* row0 = src + (long)top * sourcePitch;
                byte* row1 = row0 + sourcePitch;
                byte* yDst0 = yPlane + (long)top * width;
                byte* yDst1 = yDst0 + width;
                byte* uvDst = uvPlane + (long)pair * width;

                for (int x = 0; x < width; x += 2)
                {
                    int i0 = x * 4;
                    int i1 = i0 + 4;
                    int b00 = row0[i0], g00 = row0[i0 + 1], r00 = row0[i0 + 2];
                    int b01 = row0[i1], g01 = row0[i1 + 1], r01 = row0[i1 + 2];
                    int b10 = row1[i0], g10 = row1[i0 + 1], r10 = row1[i0 + 2];
                    int b11 = row1[i1], g11 = row1[i1 + 1], r11 = row1[i1 + 2];

                    yDst0[x] = ToY(r00, g00, b00);
                    yDst0[x + 1] = ToY(r01, g01, b01);
                    yDst1[x] = ToY(r10, g10, b10);
                    yDst1[x + 1] = ToY(r11, g11, b11);

                    int r = (r00 + r01 + r10 + r11 + 2) >> 2;
                    int g = (g00 + g01 + g10 + g11 + 2) >> 2;
                    int b = (b00 + b01 + b10 + b11 + 2) >> 2;
                    uvDst[x] = ClampByte(((-26 * r - 87 * g + 112 * b + 128) >> 8) + 128);
                    uvDst[x + 1] = ClampByte(((112 * r - 102 * g - 10 * b + 128) >> 8) + 128);
                }
            });
        }
    }

    private static byte ToY(int r, int g, int b) => ClampByte(((47 * r + 157 * g + 16 * b + 128) >> 8) + 16);

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static unsafe void CopyFromPointer(nint source, byte[] destination, int length)
    {
        const int parallelThreshold = 4 * 1024 * 1024;
        const int chunkSize = 2 * 1024 * 1024;

        if (length < parallelThreshold || Environment.ProcessorCount < 2)
        {
            fixed (byte* dst = destination)
            {
                Buffer.MemoryCopy((void*)source, dst, length, length);
            }
            return;
        }

        int chunkCount = (length + chunkSize - 1) / chunkSize;
        Parallel.For(0, chunkCount, i =>
        {
            int offset = i * chunkSize;
            int count = Math.Min(chunkSize, length - offset);
            fixed (byte* dst = &destination[offset])
            {
                Buffer.MemoryCopy((byte*)source + offset, dst, count, count);
            }
        });
    }

    private static void CopyFrame(byte[] frame, byte[] destination)
    {
        const int parallelThreshold = 4 * 1024 * 1024;
        const int chunkSize = 2 * 1024 * 1024;
        int length = frame.Length;

        if (length < parallelThreshold || Environment.ProcessorCount < 2)
        {
            frame.AsSpan().CopyTo(destination);
            return;
        }

        int chunkCount = (length + chunkSize - 1) / chunkSize;
        Parallel.For(0, chunkCount, i =>
        {
            int offset = i * chunkSize;
            int count = Math.Min(chunkSize, length - offset);
            frame.AsSpan(offset, count).CopyTo(destination.AsSpan(offset, count));
        });
    }

    private static unsafe void ConvertFloatToPcm16(float* src, short* dst, int length)
    {
        int i = 0;
        if (Avx2.IsSupported && length >= 16)
        {
            var scale = Vector256.Create(32767f);
            var min = Vector256.Create(-32767f);
            var max = Vector256.Create(32767f);
            int simdLength = length - (length % 16);
            for (; i < simdLength; i += 16)
            {
                var lo = Avx.Max(min, Avx.Min(max, Avx.Multiply(Avx.LoadVector256(src + i), scale)));
                var hi = Avx.Max(min, Avx.Min(max, Avx.Multiply(Avx.LoadVector256(src + i + 8), scale)));
                var packed = Avx2.PackSignedSaturate(Avx.ConvertToVector256Int32(lo), Avx.ConvertToVector256Int32(hi));
                Avx.Store(dst + i, Avx2.Permute4x64(packed.AsInt64(), 0b11011000).AsInt16());
            }
        }
        else if (Sse2.IsSupported && length >= 8)
        {
            var scale = Vector128.Create(32767f);
            var min = Vector128.Create(-32767f);
            var max = Vector128.Create(32767f);
            int simdLength = length - (length % 8);
            for (; i < simdLength; i += 8)
            {
                var lo = Sse.Max(min, Sse.Min(max, Sse.Multiply(Sse.LoadVector128(src + i), scale)));
                var hi = Sse.Max(min, Sse.Min(max, Sse.Multiply(Sse.LoadVector128(src + i + 4), scale)));
                Sse2.Store(dst + i, Sse2.PackSignedSaturate(Sse2.ConvertToVector128Int32(lo), Sse2.ConvertToVector128Int32(hi)));
            }
        }
        for (; i < length; i++)
            dst[i] = (short)Math.Clamp(src[i] * 32767f, -32767f, 32767f);
    }

    private long VideoFrameToTime(long frameIndex) => 10_000_000L * frameIndex / _settings.FPS;

    private long AudioPositionToTime(long samplePosition) => samplePosition / 2 * 10_000_000L / _settings.Hz;

    private long NextSequence() => Interlocked.Increment(ref _sequence) - 1;

    private void MarkSkipped(long sequence)
    {
        _completed[sequence] = SkippedSample;
        _completedSignal.Release();
    }

    private void ThrowIfFailed() => _failure?.Throw();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GCSettings.LatencyMode = _previousLatencyMode;

        _deviceDecision.TrySetResult(0);
        _rawChannel.Writer.TryComplete();
        _pipelineThread.Join();

        if (_failure is { } failure)
            Log.Default.Write(Texts.WriteFailed, failure.SourceException);
    }
}
