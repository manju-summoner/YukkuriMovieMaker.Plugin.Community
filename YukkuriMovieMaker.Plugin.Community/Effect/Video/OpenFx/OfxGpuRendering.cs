using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SharpGen.Runtime;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OpenFX GPUレンダリングの系統。
    /// </summary>
    internal enum OfxGpuRenderKind
    {
        OpenGL,
        Cuda,
        OpenCLBuffer,
        OpenCLImage,
    }

    /// <summary>
    /// GPUレンダリング中に呼び出すOFXアクション。
    /// バックエンドがコンテキストの有効化や同期を各アクションの前後に行えるよう区別する。
    /// </summary>
    internal enum OfxGpuRenderAction
    {
        BeginSequenceRender,
        Render,
        EndSequenceRender,
    }

    /// <summary>
    /// <see cref="OfxImage"/> が所有する画像ストレージ。
    /// CPUポインタ、CUDAデバイスポインタ、OpenCLメモリを
    /// 同じclipGetImage画像プロパティ構築処理へ渡すための系統非依存の抽象。
    /// </summary>
    internal interface IOfxImageStorage : IDisposable
    {
        nint DataPointer { get; }
        nint OpenCLImage { get; }
        int RowBytes { get; }
        bool IsCpuAccessible { get; }
    }

    /// <summary>
    /// OpenGL texture handleのストレージ抽象。
    /// OpenGLはclipGetImageではなくOfxImageEffectOpenGLRenderSuiteV1.clipLoadTextureで
    /// この値を返す規格のため、<see cref="IOfxImageStorage"/>とは分離する。
    /// 実際のsuite提供はフェーズ3で行う。
    /// </summary>
    internal interface IOfxOpenGlTextureStorage : IDisposable
    {
        int TextureIndex { get; }
        int TextureTarget { get; }
    }

    /// <summary>
    /// OpenFX GPUレンダリングバックエンド。
    /// フェーズ2以降のCUDA等はこのインターフェイスを実装し、
    /// CPU画像との転送・OFXアクション中のコンテキスト管理・TDR時の解放を提供する。
    /// </summary>
    internal interface IOfxGpuRenderBackend : IDisposable
    {
        OfxGpuRenderKind Kind { get; }
        bool IsAvailable { get; }
        nint CommandQueue { get; }

        void Initialize(IGraphicsDevicesAndContext devices);
        IOfxImageStorage CreateImageStorage(int width, int height, int offsetX, int offsetY, bool isOutput);
        void Upload(OfxImage cpuImage, OfxImage gpuImage);
        void Download(OfxImage gpuImage, OfxImage cpuImage);
        int ExecuteWithContext(Func<int> actionBody);
        int ExecuteAction(OfxGpuRenderAction action, OfxPropertySet inArgs, Func<int> actionBody);
        void Synchronize();
        void OnRenderFailed(int status);
        void OnBackendFailed();
        void ReleaseDeviceResources();
    }

    /// <summary>
    /// D3D11テクスチャとOpenFX GPU画像をCPUへ戻さず相互転送できるバックエンド。
    /// 登録cacheはD3D11 resourceへCOM参照を保持し、明示解放またはbackend破棄まで寿命を固定する。
    /// </summary>
    internal interface IOfxD3D11InteropBackend
    {
        bool IsD3D11InteropAvailable { get; }
        void UploadFromD3D11(nint d3d11Resource, OfxImage gpuImage);
        void DownloadToD3D11(OfxImage gpuImage, nint d3d11Resource, string preMultiplication);
        void ReleaseD3D11Resource(nint d3d11Resource);
        void ReleaseD3D11Resources();
    }

    /// <summary>
    /// GPUバックエンドの生成入口。
    /// 各OpenFXホストが同じ入口を通ることで、個別ホストへCUDA固有処理を持ち込まない。
    /// </summary>
    internal static class OfxGpuRenderBackendFactory
    {
        static readonly Lazy<bool> hasCudaBackend = new(() =>
        {
            var available = CudaDriver.TryInitialize(out var failureReason);
            if (!available)
                LogUnavailableOnce(failureReason);
            return available;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        static int hasLoggedUnavailable;

        /// <summary>
        /// 実GPUバックエンドが登録されているか。
        /// 実GPUを必要とするテストはこの値でGPUなし環境を自動スキップする。
        /// </summary>
        public static bool HasRegisteredBackend => hasCudaBackend.Value;

        public static IOfxGpuRenderBackend? Create(IGraphicsDevicesAndContext devices)
        {
            var backend = CreateCore();
            if (backend is null)
                return null;
            try
            {
                backend.Initialize(devices);
                return backend;
            }
            catch (Exception e) when (IsUnavailableException(e))
            {
                try
                {
                    // Initialize途中まで作られた共有資源も解放する。
                    // 実装は部分初期化状態で呼ばれても安全でなければならない。
                    backend.ReleaseDeviceResources();
                }
                finally
                {
                    backend.Dispose();
                }
                LogUnavailableOnce(e.Message);
                return null;
            }
        }

        static IOfxGpuRenderBackend? CreateCore()
            => HasRegisteredBackend ? new CudaGpuRenderBackend() : null;

        static bool IsUnavailableException(Exception e)
            => e is CudaException
                or CudaUnavailableException
                or DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SharpGenException;

        static void LogUnavailableOnce(string? reason)
        {
            if (Interlocked.Exchange(ref hasLoggedUnavailable, 1) != 0)
                return;
            OfxHostLog.Info($"OpenFX CUDAバックエンドを利用できないためCPUレンダリングを使用します。reason={reason}");
        }
    }

    /// <summary>
    /// CUDA Driver APIを使うOpenFX GPUバックエンド。
    /// CUDA Driver APIとD3D11 interopを使い、D2D由来のBGRA8テクスチャと
    /// OpenFXのリニアRGBA float画像をGPU内で相互変換する。
    /// interop不可時は従来のCPU画像との同期コピー経路を維持する。
    /// </summary>
    internal sealed unsafe class CudaGpuRenderBackend : IOfxGpuRenderBackend, IOfxD3D11InteropBackend
    {
        // CUDAのcurrent contextはスレッドローカルだが、OFXプラグイン側のCUDA資源管理まで含めて
        // プレビュー／出力の並行レンダーを安全側へ倒すため、全バックエンドの操作を直列化する。
        static readonly object cudaLock = new();
        static readonly Dictionary<int, nint> residentPrimaryContexts = [];
        static readonly Dictionary<int, long> residentPrimaryContextGenerations = [];
        static readonly Dictionary<int, SharedConversionResources> sharedConversionResources = [];
        static long nextResidentPrimaryContextGeneration;

        int device;
        nint context;
        long residentContextGeneration;
        nint stream;
        SharedConversionResources? conversionResources;
        SharedConversionResources? residentConversionResourcesGeneration;
        readonly Dictionary<nint, RegisteredD3D11Resource> registeredD3D11Resources = [];
        bool isD3D11InteropAvailable;
        bool hasLoggedInteropFailure;
        bool isReleased;
        bool invalidatesResidentResources;

        static CudaGpuRenderBackend()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseResidentResourcesAtProcessExit();
        }

#if DEBUG
        internal static long CompletedRenderActionCount => Interlocked.Read(ref completedRenderActionCount);
        static long completedRenderActionCount;
        internal static long CompletedD3D11InteropCount => Interlocked.Read(ref completedD3D11InteropCount);
        static long completedD3D11InteropCount;
        internal static long ResidentPrimaryContextCreationCountForTest
            => Interlocked.Read(ref residentPrimaryContextCreationCount);
        static long residentPrimaryContextCreationCount;
        internal static long ConversionModuleLoadCountForTest
            => Interlocked.Read(ref conversionModuleLoadCount);
        static long conversionModuleLoadCount;
        internal static bool ForceD3D11InteropUnavailableForTest { get; set; }
        internal static bool ForceConversionModuleFailureForTest { get; set; }
        internal static bool ForceStreamCreationFailureForTest { get; set; }
        static readonly CudaInteropTimingAccumulator timing = new();

        internal static void ResetTimingForTest() => timing.Reset();
        internal static CudaInteropTimingSnapshot GetTimingForTest() => timing.Snapshot();
        internal int RegisteredD3D11ResourceCountForTest
        {
            get
            {
                lock (cudaLock)
                    return registeredD3D11Resources.Count;
            }
        }
        internal long D3D11ResourceRegistrationCountForTest
            => Interlocked.Read(ref d3d11ResourceRegistrationCount);
        long d3d11ResourceRegistrationCount;
#endif

        public OfxGpuRenderKind Kind => OfxGpuRenderKind.Cuda;
        public bool IsAvailable => context != 0 && !isReleased;
        public nint CommandQueue => stream;
        public bool IsD3D11InteropAvailable
        {
            get
            {
                lock (cudaLock)
                {
#if DEBUG
                    if (ForceD3D11InteropUnavailableForTest)
                        return false;
#endif
                    return IsAvailable && isD3D11InteropAvailable;
                }
            }
        }

        public void Initialize(IGraphicsDevicesAndContext devices)
        {
            lock (cudaLock)
            {
                if (isReleased)
                    throw new ObjectDisposedException(nameof(CudaGpuRenderBackend));
                if (context != 0)
                    return;
                if (!CudaDriver.TryInitialize(out var failureReason))
                    throw new CudaUnavailableException(failureReason ?? "CUDAバックエンドを初期化できませんでした。");
                device = CudaDriver.GetDevice(0);
                try
                {
                    var d3d11Device = CudaDriver.GetD3D11Device(devices.DXGI.Adapter.NativePointer);
                    // CUDA列挙順がdevice 0でないマルチGPU構成でも、D3D11アダプターに
                    // 対応するCUDAデバイスのprimary contextを選べばinteropできる。
                    device = d3d11Device;
                    isD3D11InteropAvailable = true;
                }
                catch (CudaException e)
                {
                    // D3D11がIntel/AMD、CUDAがNVIDIA等の構成でもCUDAプラグイン自体は
                    // CPU転送経路で利用できるため、interopだけを無効化する。
                    isD3D11InteropAvailable = false;
                    LogInteropFailureOnce(e.Message);
                }
#if DEBUG
                var contextRetainStarted = Stopwatch.GetTimestamp();
#endif
                context = AcquireResidentPrimaryContext(device, out residentContextGeneration);
#if DEBUG
                timing.AddContextRetain(Stopwatch.GetTimestamp() - contextRetainStarted);
#endif
                try
                {
                    // CU_STREAM_DEFAULTはレガシーdefault streamを使うプラグインとの暗黙の順序保証を
                    // 維持するため意図的に使う。非blocking streamへ変更してはならない。
                    WithContext(() =>
                    {
#if DEBUG
                        if (ForceStreamCreationFailureForTest)
                            throw new CudaException(999, "テスト用CUDAストリーム作成失敗", "");
                        var streamCreateStarted = Stopwatch.GetTimestamp();
#endif
                        stream = CudaDriver.CreateStream();
#if DEBUG
                        timing.AddStreamCreate(Stopwatch.GetTimestamp() - streamCreateStarted);
#endif
                    });
                    conversionResources = AcquireSharedConversionResources(device);
                    residentConversionResourcesGeneration = conversionResources;
                }
                catch
                {
                    // stream作成後の共有資源取得失敗も含め、部分初期化した全CUDA資源を回収する。
                    invalidatesResidentResources = true;
                    ReleaseDeviceResources();
                    throw;
                }
            }
        }

        public IOfxImageStorage CreateImageStorage(int width, int height, int offsetX, int offsetY, bool isOutput)
        {
            _ = offsetX;
            _ = offsetY;
            var rowBytes = checked(width * 4 * sizeof(float));
            var byteCount = checked((nuint)((long)rowBytes * height));
#if DEBUG
            var allocationStarted = Stopwatch.GetTimestamp();
#endif
            var pointer = isOutput ? AllocateZeroed(byteCount) : Allocate(byteCount);
#if DEBUG
            timing.AddImageAllocation(Stopwatch.GetTimestamp() - allocationStarted);
#endif
            return new CudaImageStorage(this, pointer, rowBytes);
        }

        public void Upload(OfxImage cpuImage, OfxImage gpuImage)
        {
            ValidateTransfer(cpuImage, gpuImage);
            var byteCount = checked((nuint)((long)cpuImage.RowBytes * cpuImage.Height));
            WithContext(() => CudaDriver.CopyHostToDevice(
                (ulong)gpuImage.Storage.DataPointer,
                (nint)cpuImage.Data,
                byteCount));
        }

        public void Download(OfxImage gpuImage, OfxImage cpuImage)
        {
            ValidateTransfer(cpuImage, gpuImage);
            var byteCount = checked((nuint)((long)cpuImage.RowBytes * cpuImage.Height));
            WithContext(() => CudaDriver.CopyDeviceToHost(
                (nint)cpuImage.Data,
                (ulong)gpuImage.Storage.DataPointer,
                byteCount));
        }

        public void UploadFromD3D11(nint d3d11Resource, OfxImage gpuImage)
        {
            ValidateInteropImage(gpuImage);
            ExecuteInterop(d3d11Resource, array =>
            {
                var rowBytes = checked((nuint)(gpuImage.Width * 4));
                EnsureBgraScratch(checked(rowBytes * (nuint)gpuImage.Height));
#if DEBUG
                var copyStarted = Stopwatch.GetTimestamp();
#endif
                CudaDriver.CopyArrayToDeviceAsync(array, conversionResources!.BgraScratch, rowBytes, rowBytes, (nuint)gpuImage.Height, stream);
#if DEBUG
                timing.AddCopy(Stopwatch.GetTimestamp() - copyStarted);
                var conversionStarted = Stopwatch.GetTimestamp();
#endif
                LaunchBgraToRgba(gpuImage);
#if DEBUG
                timing.AddConversion(Stopwatch.GetTimestamp() - conversionStarted);
#endif
            });
        }

        public void DownloadToD3D11(OfxImage gpuImage, nint d3d11Resource, string preMultiplication)
        {
            ValidateInteropImage(gpuImage);
            ExecuteInterop(d3d11Resource, array =>
            {
                var rowBytes = checked((nuint)(gpuImage.Width * 4));
                EnsureBgraScratch(checked(rowBytes * (nuint)gpuImage.Height));
#if DEBUG
                var conversionStarted = Stopwatch.GetTimestamp();
#endif
                LaunchRgbaToBgra(gpuImage, preMultiplication);
#if DEBUG
                timing.AddConversion(Stopwatch.GetTimestamp() - conversionStarted);
                var copyStarted = Stopwatch.GetTimestamp();
#endif
                CudaDriver.CopyDeviceToArrayAsync(conversionResources!.BgraScratch, rowBytes, array, rowBytes, (nuint)gpuImage.Height, stream);
#if DEBUG
                timing.AddCopy(Stopwatch.GetTimestamp() - copyStarted);
#endif
            }, requiresD3D11Completion: true);
#if DEBUG
            Interlocked.Increment(ref completedD3D11InteropCount);
#endif
        }

        public int ExecuteAction(OfxGpuRenderAction action, OfxPropertySet inArgs, Func<int> actionBody)
        {
            _ = inArgs;
            return WithContext(() =>
            {
#if DEBUG
                var actionStarted = Stopwatch.GetTimestamp();
                var pluginStarted = actionStarted;
#endif
                var status = actionBody();
#if DEBUG
                if (action == OfxGpuRenderAction.Render)
                    timing.AddPluginRender(Stopwatch.GetTimestamp() - pluginStarted);
#endif
                // Begin/Render/Endと後続Downloadは同じblocking stream（またはそれと暗黙同期する
                // legacy default stream）へ順番に積まれる。成功時はDownload側の同期へ集約する。
                // 失敗時だけ、CPUフォールバックがGPU画像を解放する前に完了を確定する。
                if (status is not OfxStatus.OK and not OfxStatus.ReplyDefault)
                {
#if DEBUG
                    var syncStarted = Stopwatch.GetTimestamp();
#endif
                    CudaDriver.SynchronizeStream(stream);
#if DEBUG
                    timing.AddSync(Stopwatch.GetTimestamp() - syncStarted);
#endif
                }
#if DEBUG
                timing.AddGpuAction(Stopwatch.GetTimestamp() - actionStarted);
                if (action == OfxGpuRenderAction.Render && status == OfxStatus.OK)
                    Interlocked.Increment(ref completedRenderActionCount);
#endif
                return status;
            });
        }

        public int ExecuteWithContext(Func<int> actionBody)
            => WithContext(actionBody);

        public void Synchronize()
            => WithContext(() => CudaDriver.SynchronizeStream(stream));

        public void OnRenderFailed(int status)
        {
            _ = status;
            // kOfxStatGPURenderFailed / kOfxStatGPUOutOfMemory はフレーム単位の
            // CPU再試行要求であり、CUDAコンテキストや共有資源は破棄しない。
            // ただしEndSequenceRenderまでに同じstreamへ積まれた処理を完了させ、
            // 呼び出し側がGPU画像を解放してよい状態にする。
            WithContext(() => CudaDriver.SynchronizeStream(stream));
        }

        public void OnBackendFailed()
        {
            lock (cudaLock)
            {
                // 常駐資源の無効化指定とbackend解放を同じlock区間で完了させる。
                // 通常Disposeが間に入りisReleasedを先に立てると、TDR時のcontext解放を飛ばすため。
                invalidatesResidentResources = true;
                ReleaseDeviceResources();
            }
        }

        public void ReleaseD3D11Resource(nint d3d11Resource)
        {
            lock (cudaLock)
            {
                if (context == 0 || isReleased)
                    return;
                try
                {
                    CudaDriver.PushContext(context);
                    try
                    {
                        ReleaseRegisteredD3D11Resource(d3d11Resource);
                    }
                    finally
                    {
                        CudaDriver.PopContext();
                    }
                }
                catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    // TDR後の個別解除失敗でbitmapのDisposeやCPUフォールバックを止めない。
                    // 失敗したentryとCOM参照はcacheに残し、後続のbackend解放で再回収する。
                    isD3D11InteropAvailable = false;
                    OfxHostLog.Info($"OpenFX CUDA共有D3D11資源の登録解除に失敗しました。error={e.Message}");
                }
            }
        }

        public void ReleaseD3D11Resources()
        {
            lock (cudaLock)
            {
                if (registeredD3D11Resources.Count == 0)
                    return;
                if (context == 0 || isReleased)
                {
                    ReleaseCachedD3D11ComReferences();
                    return;
                }
                try
                {
                    CudaDriver.PushContext(context);
                    try
                    {
                        ReleaseRegisteredD3D11Resources();
                    }
                    finally
                    {
                        CudaDriver.PopContext();
                    }
                }
                catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    // surface取得失敗や設定OFFからのCPUフォールバックを優先する。
                    isD3D11InteropAvailable = false;
                    OfxHostLog.Info($"OpenFX CUDA共有D3D11資源cacheの解放に失敗しました。error={e.Message}");
                }
            }
        }

        public void ReleaseDeviceResources()
        {
            lock (cudaLock)
            {
                if (isReleased)
                {
                    // 通常DisposeがTDR通知より先に完了していても、device単位の常駐資源は
                    // static cacheから辿って無効化できる。後着の失敗通知を捨ててはならない。
                    if (invalidatesResidentResources)
                        InvalidateResidentResourcesAfterBackendFailure();
                    return;
                }
                isReleased = true;
                if (context == 0)
                {
                    ReleaseCachedD3D11ComReferences();
                    return;
                }
                try
                {
                    CudaDriver.PushContext(context);
                    try
                    {
                        ReleaseRegisteredD3D11Resources();
                        if (conversionResources is not null)
                        {
                            var resources = conversionResources;
                            conversionResources = null;
                            try
                            {
                                ReleaseSharedConversionResources(device, resources);
                            }
                            catch (Exception e)
                            {
                                OfxHostLog.Info($"OpenFX CUDA共有変換資源の解放に失敗しました。error={e.Message}");
                            }
                        }
                        if (stream != 0)
                        {
                            try
                            {
#if DEBUG
                                var streamDestroyStarted = Stopwatch.GetTimestamp();
#endif
                                CudaDriver.DestroyStream(stream);
#if DEBUG
                                timing.AddStreamDestroy(Stopwatch.GetTimestamp() - streamDestroyStarted);
#endif
                            }
                            catch (Exception e)
                            {
                                OfxHostLog.Info($"OpenFX CUDAストリームの破棄に失敗しました。error={e.Message}");
                            }
                            finally
                            {
                                stream = 0;
                            }
                        }
                    }
                    finally
                    {
                        CudaDriver.PopContext();
                    }
                }
                catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    OfxHostLog.Info($"OpenFX CUDA資源の解放中にエラーが発生しました。error={e.Message}");
                }
                finally
                {
                    if (invalidatesResidentResources)
                        InvalidateResidentResourcesAfterBackendFailure();
                    context = 0;
                    stream = 0;
                    isD3D11InteropAvailable = false;
                    // unregisterに失敗したentryもhostが追加したCOM参照だけは回収する。
                    // CUDA側の登録状態はこの時点では確認・解除できないため、entryを破棄して
                    // 後続バックエンドから再利用されないようにする。
                    ReleaseCachedD3D11ComReferences();
                }
            }
        }

        public void Dispose() => ReleaseDeviceResources();

        // cudaLock内から呼び出す。通常Dispose後はinstanceのcontextが0でも、
        // device単位のstatic cacheに残る常駐contextを使ってTDR無効化を完了する。
        void InvalidateResidentResourcesAfterBackendFailure()
        {
            var expectedGeneration = residentContextGeneration;
            var expectedResources = residentConversionResourcesGeneration;
            if (expectedGeneration == 0
                || !residentPrimaryContexts.TryGetValue(device, out var residentContext)
                || !residentPrimaryContextGenerations.TryGetValue(device, out var currentGeneration)
                || currentGeneration != expectedGeneration
                || (expectedResources is not null
                    && (!sharedConversionResources.TryGetValue(device, out var currentResources)
                        || !ReferenceEquals(currentResources, expectedResources))))
            {
                // 別のTDR処理が既に再構築した新世代を、古いbackendの遅延通知で無効化しない。
                return;
            }
            try
            {
                InvalidateResidentConversionResources(device, residentContext);
            }
            catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
            {
                // context自体を失っていても、無効な共有ハンドルを後続backendへ渡さない。
                try
                {
                    if (sharedConversionResources.TryGetValue(device, out var resources))
                        InvalidateSharedConversionResources(device, resources);
                }
                catch (Exception cleanupException) when (cleanupException is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    OfxHostLog.Info($"OpenFX CUDA共有変換資源の論理無効化中にエラーが発生しました。error={cleanupException.Message}");
                }
                OfxHostLog.Info($"OpenFX CUDA共有変換資源の無効化に失敗しました。error={e.Message}");
            }
            try
            {
#if DEBUG
                var contextReleaseStarted = Stopwatch.GetTimestamp();
#endif
                ReleaseResidentPrimaryContext(device, residentContext, expectedGeneration);
#if DEBUG
                timing.AddContextRelease(Stopwatch.GetTimestamp() - contextReleaseStarted);
#endif
            }
            catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
            {
                OfxHostLog.Info($"OpenFX CUDAプライマリコンテキストの無効化に失敗しました。error={e.Message}");
            }
        }

        ulong Allocate(nuint byteCount)
            => WithContext(() => CudaDriver.Allocate(byteCount));

        ulong AllocateZeroed(nuint byteCount)
            => WithContext(() =>
            {
                var pointer = CudaDriver.Allocate(byteCount);
                try
                {
                    CudaDriver.MemsetD8(pointer, 0, byteCount);
                    return pointer;
                }
                catch
                {
                    CudaDriver.Free(pointer);
                    throw;
                }
            });

        void Free(ulong pointer)
        {
            if (pointer == 0)
                return;
            lock (cudaLock)
            {
                if (context == 0 || isReleased)
                {
                    // バックエンド解放後の画像破棄ではコンテキストが既に無いため個別Freeできない。
                    // TDR時に残り得る量は、その時点で生存していた画像プール分を上限とする。
                    return;
                }
                try
                {
                    CudaDriver.PushContext(context);
                    try
                    {
#if DEBUG
                        var imageFreeStarted = Stopwatch.GetTimestamp();
#endif
                        CudaDriver.Free(pointer);
#if DEBUG
                        timing.AddImageFree(Stopwatch.GetTimestamp() - imageFreeStarted);
#endif
                    }
                    finally
                    {
                        CudaDriver.PopContext();
                    }
                }
                catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    // TDR後は個別メモリ解放も失敗しうる。後続画像の破棄とCPUフォールバックを
                    // 止めず、プライマリコンテキスト解放へ進ませる。
                    OfxHostLog.Info($"OpenFX CUDA画像の解放に失敗しました。error={e.Message}");
                }
            }
        }

        T WithContext<T>(Func<T> action)
        {
            lock (cudaLock)
            {
                EnsureAvailable();
                CudaDriver.PushContext(context);
                try
                {
                    return action();
                }
                finally
                {
                    CudaDriver.PopContext();
                }
            }
        }

        void WithContext(Action action)
            => WithContext(() =>
            {
                action();
                return true;
            });

        void EnsureAvailable()
        {
            if (!IsAvailable)
                throw new InvalidOperationException("CUDAバックエンドは既に解放されています。");
        }

        void ExecuteInterop(nint d3d11Resource, Action<nint> operation, bool requiresD3D11Completion = false)
        {
            if (!IsD3D11InteropAvailable)
                throw new CudaInteropUnavailableException("CUDAとD3D11のinteropを利用できません。");
            WithContext(() =>
            {
                RegisteredD3D11Resource registeredResource;
                try
                {
#if DEBUG
                    var registerStarted = Stopwatch.GetTimestamp();
#endif
                    registeredResource = GetOrRegisterD3D11Resource(d3d11Resource);
#if DEBUG
                    timing.AddRegister(Stopwatch.GetTimestamp() - registerStarted);
#endif
                }
                catch (CudaException e)
                {
                    // D3D11リソースの登録失敗だけをinterop基盤の失敗として記録する。
                    isD3D11InteropAvailable = false;
                    ReleaseRegisteredD3D11Resources();
                    LogInteropFailureOnce(e.Message);
                    throw new CudaInteropUnavailableException(e.Message, e);
                }
                var isMapped = false;
                var operationCompleted = false;
                try
                {
#if DEBUG
                    var mapStarted = Stopwatch.GetTimestamp();
#endif
                    CudaDriver.MapGraphicsResource(registeredResource.GraphicsResource, stream);
#if DEBUG
                    timing.AddMap(Stopwatch.GetTimestamp() - mapStarted);
#endif
                    isMapped = true;
                    operation(CudaDriver.GetMappedArray(registeredResource.GraphicsResource));
                    // operationは非同期コピーと変換カーネルを同じstreamへ積む。
                    // device共有scratchはbackendごとのstream間でcudaLockだけを使って直列化するため、
                    // lockを抜ける前にこのstreamのscratch利用を完了させる。unmap自体はstream順序付き。
#if DEBUG
                    var syncStarted = Stopwatch.GetTimestamp();
#endif
                    CudaDriver.SynchronizeStream(stream);
#if DEBUG
                    timing.AddSync(Stopwatch.GetTimestamp() - syncStarted);
#endif
                    operationCompleted = true;
                }
                finally
                {
                    if (isMapped)
                    {
                        if (!operationCompleted)
                        {
                            try
                            {
#if DEBUG
                                var recoverySyncStarted = Stopwatch.GetTimestamp();
#endif
                                CudaDriver.SynchronizeStream(stream);
#if DEBUG
                                timing.AddSync(Stopwatch.GetTimestamp() - recoverySyncStarted);
#endif
                            }
                            catch (CudaException)
                            {
                                // 元のCUDA例外を維持しつつ、可能な限りunmapへ進む。
                            }
                        }
#if DEBUG
                        var unmapStarted = Stopwatch.GetTimestamp();
#endif
                        CudaDriver.UnmapGraphicsResource(registeredResource.GraphicsResource, stream);
#if DEBUG
                        timing.AddUnmap(Stopwatch.GetTimestamp() - unmapStarted);
#endif
                        if (operationCompleted && requiresD3D11Completion)
#if DEBUG
                        {
                            // unmapまでを完了させてD3D11側へ所有権を戻すと同時に、
                            // 非同期kernel/copy失敗をこのレンダー呼び出し内で報告する境界を作る。
                            var finalSyncStarted = Stopwatch.GetTimestamp();
                            CudaDriver.SynchronizeStream(stream);
                            timing.AddSync(Stopwatch.GetTimestamp() - finalSyncStarted);
                        }
#else
                            // D3D11側へ所有権を戻し、非同期kernel/copy失敗をこの呼び出し内で報告する。
                            CudaDriver.SynchronizeStream(stream);
#endif
                    }
                }
            });
        }

        RegisteredD3D11Resource GetOrRegisterD3D11Resource(nint d3d11Resource)
        {
            if (registeredD3D11Resources.TryGetValue(d3d11Resource, out var registered))
                return registered;

            // CUDA登録中にD2D側ラッパーが破棄されてもネイティブresourceが消えないよう、
            // cache entry自身がCOM参照を1つ保持する。通常のサイズ変更・Disposeでは
            // OfxD3D11Interop.ReleaseResourceが即時解放し、漏れた場合もbackend破棄が上限となる。
            Marshal.AddRef(d3d11Resource);
            try
            {
                registered = new RegisteredD3D11Resource(
                    d3d11Resource,
                    CudaDriver.RegisterD3D11Resource(d3d11Resource));
                registeredD3D11Resources.Add(d3d11Resource, registered);
#if DEBUG
                Interlocked.Increment(ref d3d11ResourceRegistrationCount);
#endif
                return registered;
            }
            catch
            {
                Marshal.Release(d3d11Resource);
                throw;
            }
        }

        void ReleaseRegisteredD3D11Resource(nint d3d11Resource)
        {
            if (!registeredD3D11Resources.TryGetValue(d3d11Resource, out var registered))
                return;
            // map/unmap は同じストリームへ投入されるため、unregister 前に
            // 直前の unmap まで完了させる。
            CudaDriver.SynchronizeStream(stream);
            UnregisterD3D11Resource(d3d11Resource, registered);
        }

        void UnregisterD3D11Resource(nint d3d11Resource, RegisteredD3D11Resource registered)
        {
#if DEBUG
            var unregisterStarted = Stopwatch.GetTimestamp();
#endif
            CudaDriver.UnregisterGraphicsResource(registered.GraphicsResource);
            registeredD3D11Resources.Remove(d3d11Resource);
            Marshal.Release(registered.D3D11Resource);
#if DEBUG
            timing.AddUnregister(Stopwatch.GetTimestamp() - unregisterStarted);
#endif
        }

        void ReleaseRegisteredD3D11Resources()
        {
            if (registeredD3D11Resources.Count == 0)
                return;
            // unregisterはstream順序付きではないため、全entryに対して一度だけ
            // 同期し、各resourceの直前のunmapまで完了させてから一括解除する。
            CudaDriver.SynchronizeStream(stream);
            foreach (var d3d11Resource in registeredD3D11Resources.Keys.ToArray())
            {
                try
                {
                    var registered = registeredD3D11Resources[d3d11Resource];
                    UnregisterD3D11Resource(d3d11Resource, registered);
                }
                catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                {
                    OfxHostLog.Info($"OpenFX CUDA共有D3D11資源の登録解除に失敗しました。error={e.Message}");
                }
            }
        }

        void ReleaseCachedD3D11ComReferences()
        {
            foreach (var registered in registeredD3D11Resources.Values)
                Marshal.Release(registered.D3D11Resource);
            registeredD3D11Resources.Clear();
        }

        void EnsureBgraScratch(nuint byteCount)
        {
            // scratchはdevice単位で全バックエンドが共有する。各バックエンドは別streamを
            // 持つため、cudaLockを抜ける前のstream同期が完了していることを前提に再利用する。
            EnsureConversionModule();
            var resources = conversionResources!;
            if (resources.BgraScratchSize >= byteCount)
                return;
            if (resources.BgraScratch != 0)
            {
                try
                {
                    CudaDriver.Free(resources.BgraScratch);
                }
                finally
                {
                    resources.BgraScratch = 0;
                    resources.BgraScratchSize = 0;
                }
            }
#if DEBUG
            var scratchAllocationStarted = Stopwatch.GetTimestamp();
#endif
            resources.BgraScratch = CudaDriver.Allocate(byteCount);
#if DEBUG
            timing.AddScratchAllocation(Stopwatch.GetTimestamp() - scratchAllocationStarted);
#endif
            resources.BgraScratchSize = byteCount;
        }

        void EnsureConversionModule()
        {
            var resources = EnsureCurrentSharedConversionResources();
            try
            {
#if DEBUG
                if (ForceConversionModuleFailureForTest)
                    throw new CudaException(999, "テスト用CUDA変換モジュール読み込み失敗", "");
#endif
                if (resources.ConversionModule != 0)
                    return;
#if DEBUG
                var moduleLoadStarted = Stopwatch.GetTimestamp();
#endif
                resources.ConversionModule = CudaDriver.LoadModule(ConversionPtx);
#if DEBUG
                Interlocked.Increment(ref conversionModuleLoadCount);
#endif
                resources.BgraToRgbaFunction = CudaDriver.GetFunction(resources.ConversionModule, "ymm4_bgra_to_rgba");
                resources.RgbaToBgraFunction = CudaDriver.GetFunction(resources.ConversionModule, "ymm4_rgba_to_bgra");
#if DEBUG
                timing.AddModuleLoad(Stopwatch.GetTimestamp() - moduleLoadStarted);
#endif
            }
            catch (CudaException e)
            {
                try
                {
                    if (resources.ConversionModule != 0)
                    {
                        try
                        {
                            CudaDriver.UnloadModule(resources.ConversionModule);
                        }
                        catch (CudaException)
                        {
                            // 元のJIT/関数取得失敗をinterop専用例外として維持する。
                        }
                    }
                }
                finally
                {
                    resources.ConversionModule = 0;
                    resources.BgraToRgbaFunction = 0;
                    resources.RgbaToBgraFunction = 0;
                }
                isD3D11InteropAvailable = false;
                LogInteropFailureOnce(e.Message);
                throw new CudaInteropUnavailableException(e.Message, e);
            }
        }

        void LaunchBgraToRgba(OfxImage gpuImage)
        {
            var resources = conversionResources!;
            var source = resources.BgraScratch;
            var destination = (ulong)gpuImage.Storage.DataPointer;
            var width = (uint)gpuImage.Width;
            var height = (uint)gpuImage.Height;
            void*[] parameters = [&source, &destination, &width, &height];
            fixed (void** parameterPointer = parameters)
            {
                var pixels = checked(width * height);
                CudaDriver.LaunchKernel(resources.BgraToRgbaFunction, (pixels + 255) / 256, 256, stream, parameterPointer);
            }
        }

        void LaunchRgbaToBgra(OfxImage gpuImage, string preMultiplication)
        {
            var source = (ulong)gpuImage.Storage.DataPointer;
            var resources = conversionResources!;
            var destination = resources.BgraScratch;
            var width = (uint)gpuImage.Width;
            var height = (uint)gpuImage.Height;
            var mode = preMultiplication switch
            {
                OfxConstants.ImageUnPreMultiplied => 1u,
                OfxConstants.ImageOpaque => 2u,
                _ => 0u,
            };
            void*[] parameters = [&source, &destination, &width, &height, &mode];
            fixed (void** parameterPointer = parameters)
            {
                var pixels = checked(width * height);
                CudaDriver.LaunchKernel(resources.RgbaToBgraFunction, (pixels + 255) / 256, 256, stream, parameterPointer);
            }
        }

        void LogInteropFailureOnce(string reason)
        {
            // cudaLock内からだけ呼び出す。
            if (hasLoggedInteropFailure)
                return;
            hasLoggedInteropFailure = true;
            OfxHostLog.Info($"OpenFX CUDA×D3D11 interopを利用できないためCPU転送経路を使用します。reason={reason}");
        }

        static SharedConversionResources AcquireSharedConversionResources(int device)
        {
            if (!sharedConversionResources.TryGetValue(device, out var resources))
            {
                resources = new SharedConversionResources();
                sharedConversionResources.Add(device, resources);
            }
            resources.ReferenceCount++;
            return resources;
        }

        static void ReleaseSharedConversionResources(int device, SharedConversionResources resources)
        {
            resources.ReferenceCount--;
            if (resources.ReferenceCount < 0)
                throw new InvalidOperationException("CUDA共有変換資源の参照数が不正です。");
            // 参照数0でも通常のインスタンス破棄では保持する。サイズ変更で次の
            // インスタンスが直ちに作られる場合にPTX JITとscratch再確保を繰り返さないため。
            _ = device;
        }

        SharedConversionResources EnsureCurrentSharedConversionResources()
        {
            var resources = conversionResources
                ?? throw new InvalidOperationException("CUDA共有変換資源が初期化されていません。");
            if (!resources.IsInvalidated)
                return resources;
            throw new CudaUnavailableException("CUDA共有変換資源は無効化されています。新しい描画グラフで再構築してください。");
        }

        static nint AcquireResidentPrimaryContext(int device, out long generation)
        {
            if (residentPrimaryContexts.TryGetValue(device, out var residentContext))
            {
                generation = residentPrimaryContextGenerations[device];
                return residentContext;
            }
            residentContext = CudaDriver.RetainPrimaryContext(device);
            generation = ++nextResidentPrimaryContextGeneration;
#if DEBUG
            Interlocked.Increment(ref residentPrimaryContextCreationCount);
#endif
            residentPrimaryContexts.Add(device, residentContext);
            residentPrimaryContextGenerations.Add(device, generation);
            return residentContext;
        }

        static void ReleaseResidentPrimaryContext(int device, nint expectedContext, long expectedGeneration)
        {
            if (!residentPrimaryContexts.TryGetValue(device, out var residentContext)
                || residentContext != expectedContext
                || !residentPrimaryContextGenerations.TryGetValue(device, out var generation)
                || generation != expectedGeneration)
            {
                return;
            }
            residentPrimaryContexts.Remove(device);
            residentPrimaryContextGenerations.Remove(device);
            CudaDriver.ReleasePrimaryContext(device);
        }

        static void InvalidateResidentConversionResources(int device, nint context)
        {
            if (!sharedConversionResources.TryGetValue(device, out var resources))
                return;
            var contextPushed = false;
            try
            {
                CudaDriver.PushContext(context);
                contextPushed = true;
                InvalidateSharedConversionResources(device, resources);
            }
            finally
            {
                if (contextPushed)
                    CudaDriver.PopContext();
            }
        }

        static void ReleaseResidentResourcesAtProcessExit()
        {
            lock (cudaLock)
            {
                foreach (var pair in residentPrimaryContexts.ToArray())
                {
                    var contextPushed = false;
                    try
                    {
                        CudaDriver.PushContext(pair.Value);
                        contextPushed = true;
                        if (sharedConversionResources.TryGetValue(pair.Key, out var resources))
                            InvalidateSharedConversionResources(pair.Key, resources);
                    }
                    catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                    {
                        OfxHostLog.Info($"OpenFX CUDA常駐資源の終了処理に失敗しました。error={e.Message}");
                    }
                    finally
                    {
                        if (contextPushed)
                        {
                            try
                            {
                                CudaDriver.PopContext();
                            }
                            catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                            {
                                OfxHostLog.Info($"OpenFX CUDA終了時のコンテキスト復元に失敗しました。error={e.Message}");
                            }
                        }
                        try
                        {
                            CudaDriver.ReleasePrimaryContext(pair.Key);
                        }
                        catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException)
                        {
                            OfxHostLog.Info($"OpenFX CUDA常駐コンテキストの終了処理に失敗しました。error={e.Message}");
                        }
                    }
                }
                residentPrimaryContexts.Clear();
                residentPrimaryContextGenerations.Clear();
                sharedConversionResources.Clear();
            }
        }

        static void InvalidateSharedConversionResources(int device, SharedConversionResources resources)
        {
            if (resources.IsInvalidated)
                return;
            resources.IsInvalidated = true;
            if (sharedConversionResources.TryGetValue(device, out var current)
                && ReferenceEquals(current, resources))
            {
                sharedConversionResources.Remove(device);
            }
            try
            {
                if (resources.BgraScratch != 0)
                    CudaDriver.Free(resources.BgraScratch);
            }
            catch (CudaException)
            {
            }
            finally
            {
                resources.BgraScratch = 0;
                resources.BgraScratchSize = 0;
            }
            try
            {
                if (resources.ConversionModule != 0)
                    CudaDriver.UnloadModule(resources.ConversionModule);
            }
            catch (CudaException)
            {
            }
            finally
            {
                resources.ConversionModule = 0;
                resources.BgraToRgbaFunction = 0;
                resources.RgbaToBgraFunction = 0;
            }
        }

        sealed class SharedConversionResources
        {
            // moduleとscratchはdevice単位の常駐資源。scratchを使う非同期処理は
            // cudaLock内で発行し、lockを解放する前にそのbackendのstreamを同期する。
            public int ReferenceCount;
            public bool IsInvalidated;
            public nint ConversionModule;
            public nint BgraToRgbaFunction;
            public nint RgbaToBgraFunction;
            public ulong BgraScratch;
            public nuint BgraScratchSize;
        }

        static void ValidateInteropImage(OfxImage gpuImage)
        {
            if (gpuImage.Storage.IsCpuAccessible
                || gpuImage.Width <= 0
                || gpuImage.Height <= 0
                || gpuImage.RowBytes != gpuImage.Width * 4 * sizeof(float))
            {
                throw new InvalidOperationException("CUDA interop画像の形式がRGBA floatリニア画像ではありません。");
            }
        }

        readonly record struct RegisteredD3D11Resource(nint D3D11Resource, nint GraphicsResource);

#if DEBUG
        internal readonly record struct CudaInteropTimingSnapshot(
            long ContextRetainTicks,
            long ContextReleaseTicks,
            long ModuleLoadTicks,
            long StreamCreateTicks,
            long StreamDestroyTicks,
            long ScratchAllocationTicks,
            long ImageAllocationTicks,
            long ImageFreeTicks,
            long RegisterTicks,
            long MapTicks,
            long CopyTicks,
            long ConversionTicks,
            long PluginRenderTicks,
            long SyncTicks,
            long UnmapTicks,
            long UnregisterTicks,
            long GpuActionTicks,
            long SyncCount)
        {
            public static double ToMilliseconds(long ticks)
                => ticks * 1000.0 / Stopwatch.Frequency;
        }

        sealed class CudaInteropTimingAccumulator
        {
            long contextRetainTicks;
            long contextReleaseTicks;
            long moduleLoadTicks;
            long streamCreateTicks;
            long streamDestroyTicks;
            long scratchAllocationTicks;
            long imageAllocationTicks;
            long imageFreeTicks;
            long registerTicks;
            long mapTicks;
            long copyTicks;
            long conversionTicks;
            long pluginRenderTicks;
            long syncTicks;
            long unmapTicks;
            long unregisterTicks;
            long gpuActionTicks;
            long syncCount;

            public void AddContextRetain(long ticks) => Interlocked.Add(ref contextRetainTicks, ticks);
            public void AddContextRelease(long ticks) => Interlocked.Add(ref contextReleaseTicks, ticks);
            public void AddModuleLoad(long ticks) => Interlocked.Add(ref moduleLoadTicks, ticks);
            public void AddStreamCreate(long ticks) => Interlocked.Add(ref streamCreateTicks, ticks);
            public void AddStreamDestroy(long ticks) => Interlocked.Add(ref streamDestroyTicks, ticks);
            public void AddScratchAllocation(long ticks) => Interlocked.Add(ref scratchAllocationTicks, ticks);
            public void AddImageAllocation(long ticks) => Interlocked.Add(ref imageAllocationTicks, ticks);
            public void AddImageFree(long ticks) => Interlocked.Add(ref imageFreeTicks, ticks);
            public void AddRegister(long ticks) => Interlocked.Add(ref registerTicks, ticks);
            public void AddMap(long ticks) => Interlocked.Add(ref mapTicks, ticks);
            public void AddCopy(long ticks) => Interlocked.Add(ref copyTicks, ticks);
            public void AddConversion(long ticks) => Interlocked.Add(ref conversionTicks, ticks);
            public void AddPluginRender(long ticks) => Interlocked.Add(ref pluginRenderTicks, ticks);
            public void AddSync(long ticks)
            {
                Interlocked.Add(ref syncTicks, ticks);
                Interlocked.Increment(ref syncCount);
            }
            public void AddUnmap(long ticks) => Interlocked.Add(ref unmapTicks, ticks);
            public void AddUnregister(long ticks) => Interlocked.Add(ref unregisterTicks, ticks);
            public void AddGpuAction(long ticks) => Interlocked.Add(ref gpuActionTicks, ticks);

            public CudaInteropTimingSnapshot Snapshot()
                => new(
                    Interlocked.Read(ref contextRetainTicks),
                    Interlocked.Read(ref contextReleaseTicks),
                    Interlocked.Read(ref moduleLoadTicks),
                    Interlocked.Read(ref streamCreateTicks),
                    Interlocked.Read(ref streamDestroyTicks),
                    Interlocked.Read(ref scratchAllocationTicks),
                    Interlocked.Read(ref imageAllocationTicks),
                    Interlocked.Read(ref imageFreeTicks),
                    Interlocked.Read(ref registerTicks),
                    Interlocked.Read(ref mapTicks),
                    Interlocked.Read(ref copyTicks),
                    Interlocked.Read(ref conversionTicks),
                    Interlocked.Read(ref pluginRenderTicks),
                    Interlocked.Read(ref syncTicks),
                    Interlocked.Read(ref unmapTicks),
                    Interlocked.Read(ref unregisterTicks),
                    Interlocked.Read(ref gpuActionTicks),
                    Interlocked.Read(ref syncCount));

            public void Reset()
            {
                Interlocked.Exchange(ref contextRetainTicks, 0);
                Interlocked.Exchange(ref contextReleaseTicks, 0);
                Interlocked.Exchange(ref moduleLoadTicks, 0);
                Interlocked.Exchange(ref streamCreateTicks, 0);
                Interlocked.Exchange(ref streamDestroyTicks, 0);
                Interlocked.Exchange(ref scratchAllocationTicks, 0);
                Interlocked.Exchange(ref imageAllocationTicks, 0);
                Interlocked.Exchange(ref imageFreeTicks, 0);
                Interlocked.Exchange(ref registerTicks, 0);
                Interlocked.Exchange(ref mapTicks, 0);
                Interlocked.Exchange(ref copyTicks, 0);
                Interlocked.Exchange(ref conversionTicks, 0);
                Interlocked.Exchange(ref pluginRenderTicks, 0);
                Interlocked.Exchange(ref syncTicks, 0);
                Interlocked.Exchange(ref unmapTicks, 0);
                Interlocked.Exchange(ref unregisterTicks, 0);
                Interlocked.Exchange(ref gpuActionTicks, 0);
                Interlocked.Exchange(ref syncCount, 0);
            }
        }
#endif

        static void ValidateTransfer(OfxImage cpuImage, OfxImage gpuImage)
        {
            if (!cpuImage.Storage.IsCpuAccessible
                || gpuImage.Storage.IsCpuAccessible
                || cpuImage.Width != gpuImage.Width
                || cpuImage.Height != gpuImage.Height
                || cpuImage.RowBytes != gpuImage.RowBytes)
            {
                throw new InvalidOperationException("CUDA画像転送の画像形式または行ピッチが一致しません。");
            }
        }

        sealed class CudaImageStorage : IOfxImageStorage
        {
            CudaGpuRenderBackend? owner;
            ulong pointer;

            public nint DataPointer => (nint)pointer;
            public nint OpenCLImage => 0;
            public int RowBytes { get; }
            public bool IsCpuAccessible => false;

            public CudaImageStorage(CudaGpuRenderBackend owner, ulong pointer, int rowBytes)
            {
                this.owner = owner;
                this.pointer = pointer;
                RowBytes = rowBytes;
            }

            public void Dispose()
            {
                if (pointer == 0)
                    return;
                owner?.Free(pointer);
                pointer = 0;
                owner = null;
            }
        }

        const string ConversionPtx = """
.version 6.0
.target sm_50
.address_size 64

.visible .entry ymm4_bgra_to_rgba(
    .param .u64 source,
    .param .u64 destination,
    .param .u32 width,
    .param .u32 height)
{
    .reg .pred %p<2>;
    .reg .b32 %r<16>;
    .reg .b64 %rd<8>;
    .reg .f32 %f<5>;

    ld.param.u64 %rd1, [source];
    ld.param.u64 %rd2, [destination];
    ld.param.u32 %r1, [width];
    ld.param.u32 %r2, [height];
    mov.u32 %r3, %ctaid.x;
    mov.u32 %r4, %ntid.x;
    mov.u32 %r5, %tid.x;
    mad.lo.u32 %r6, %r3, %r4, %r5;
    mul.lo.u32 %r7, %r1, %r2;
    setp.ge.u32 %p1, %r6, %r7;
    @%p1 bra BGRA_DONE;

    div.u32 %r8, %r6, %r1;
    rem.u32 %r9, %r6, %r1;
    sub.u32 %r10, %r2, 1;
    sub.u32 %r10, %r10, %r8;
    mad.lo.u32 %r11, %r10, %r1, %r9;
    mul.wide.u32 %rd3, %r6, 4;
    mul.wide.u32 %rd4, %r11, 16;
    add.u64 %rd5, %rd1, %rd3;
    add.u64 %rd6, %rd2, %rd4;

    ld.global.u8 %r12, [%rd5+0];
    ld.global.u8 %r13, [%rd5+1];
    ld.global.u8 %r14, [%rd5+2];
    ld.global.u8 %r15, [%rd5+3];
    cvt.rn.f32.u32 %f1, %r14;
    cvt.rn.f32.u32 %f2, %r13;
    cvt.rn.f32.u32 %f3, %r12;
    cvt.rn.f32.u32 %f4, %r15;
    mul.rn.f32 %f1, %f1, 0f3B808081;
    mul.rn.f32 %f2, %f2, 0f3B808081;
    mul.rn.f32 %f3, %f3, 0f3B808081;
    mul.rn.f32 %f4, %f4, 0f3B808081;
    st.global.f32 [%rd6+0], %f1;
    st.global.f32 [%rd6+4], %f2;
    st.global.f32 [%rd6+8], %f3;
    st.global.f32 [%rd6+12], %f4;

BGRA_DONE:
    ret;
}

.visible .entry ymm4_rgba_to_bgra(
    .param .u64 source,
    .param .u64 destination,
    .param .u32 width,
    .param .u32 height,
    .param .u32 mode)
{
    .reg .pred %p<4>;
    .reg .b32 %r<18>;
    .reg .b64 %rd<8>;
    .reg .f32 %f<9>;

    ld.param.u64 %rd1, [source];
    ld.param.u64 %rd2, [destination];
    ld.param.u32 %r1, [width];
    ld.param.u32 %r2, [height];
    ld.param.u32 %r3, [mode];
    mov.u32 %r4, %ctaid.x;
    mov.u32 %r5, %ntid.x;
    mov.u32 %r6, %tid.x;
    mad.lo.u32 %r7, %r4, %r5, %r6;
    mul.lo.u32 %r8, %r1, %r2;
    setp.ge.u32 %p1, %r7, %r8;
    @%p1 bra RGBA_DONE;

    div.u32 %r9, %r7, %r1;
    rem.u32 %r10, %r7, %r1;
    sub.u32 %r11, %r2, 1;
    sub.u32 %r11, %r11, %r9;
    mad.lo.u32 %r12, %r11, %r1, %r10;
    mul.wide.u32 %rd3, %r12, 16;
    mul.wide.u32 %rd4, %r7, 4;
    add.u64 %rd5, %rd1, %rd3;
    add.u64 %rd6, %rd2, %rd4;

    ld.global.f32 %f1, [%rd5+0];
    ld.global.f32 %f2, [%rd5+4];
    ld.global.f32 %f3, [%rd5+8];
    ld.global.f32 %f4, [%rd5+12];
    setp.eq.u32 %p2, %r3, 1;
    @!%p2 bra NOT_UNPREMULTIPLIED;
    mul.rn.f32 %f1, %f1, %f4;
    mul.rn.f32 %f2, %f2, %f4;
    mul.rn.f32 %f3, %f3, %f4;

NOT_UNPREMULTIPLIED:
    mov.f32 %f5, 0f437F0000;
    mov.f32 %f6, 0f3F000000;
    mov.f32 %f7, 0f00000000;
    mul.rn.f32 %f1, %f1, %f5;
    mul.rn.f32 %f2, %f2, %f5;
    mul.rn.f32 %f3, %f3, %f5;
    mul.rn.f32 %f4, %f4, %f5;
    add.rn.f32 %f1, %f1, %f6;
    add.rn.f32 %f2, %f2, %f6;
    add.rn.f32 %f3, %f3, %f6;
    add.rn.f32 %f4, %f4, %f6;
    max.f32 %f1, %f1, %f7;
    max.f32 %f2, %f2, %f7;
    max.f32 %f3, %f3, %f7;
    max.f32 %f4, %f4, %f7;
    min.f32 %f1, %f1, %f5;
    min.f32 %f2, %f2, %f5;
    min.f32 %f3, %f3, %f5;
    min.f32 %f4, %f4, %f5;
    cvt.rzi.u32.f32 %r13, %f1;
    cvt.rzi.u32.f32 %r14, %f2;
    cvt.rzi.u32.f32 %r15, %f3;
    cvt.rzi.u32.f32 %r16, %f4;
    setp.eq.u32 %p3, %r3, 2;
    @!%p3 bra STORE_ALPHA;
    mov.u32 %r16, 255;

STORE_ALPHA:
    st.global.u8 [%rd6+0], %r15;
    st.global.u8 [%rd6+1], %r14;
    st.global.u8 [%rd6+2], %r13;
    st.global.u8 [%rd6+3], %r16;

RGBA_DONE:
    ret;
}
""";
    }

    /// <summary>CUDAドライバーまたは対応デバイスを利用できない。</summary>
    internal sealed class CudaUnavailableException(string message) : Exception(message)
    {
    }

    /// <summary>D3D11共有だけが利用できず、CPU転送CUDA経路へ切り替え可能な失敗。</summary>
    internal sealed class CudaInteropUnavailableException : Exception
    {
        public CudaInteropUnavailableException(string message)
            : base(message)
        {
        }

        public CudaInteropUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
