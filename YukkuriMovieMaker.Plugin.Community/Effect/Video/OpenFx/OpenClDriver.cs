using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OpenCL 1.1 API（OpenCL.dll）の最小動的バインディング層。
    /// OpenCLランタイムがない環境でも型の初期化だけでは失敗しない。
    /// </summary>
    internal static unsafe class OpenClDriver
    {
        const string LibraryName = "OpenCL.dll";
        const ulong DeviceTypeGpu = 1UL << 2;
        const ulong MemReadWrite = 1UL << 0;
        const uint PlatformExtensions = 0x0904;
        const nint ContextPlatform = 0x1084;
        const nint ContextD3D11Device = 0x401D;
        const uint D3D11DxgiAdapter = 0x401A;
        const uint PreferredDevicesForD3D11 = 0x401B;
        const uint ProgramBuildLog = 0x1183;
        static readonly object loadLock = new();
        static Api? api;
        static nint selectedGpuDevice;

        internal const int Success = 0;
        internal const int DeviceNotFound = -1;
        internal const int OutOfHostMemory = -6;
        internal const int MemObjectAllocationFailure = -4;
        internal const int OutOfResources = -5;

#if DEBUG
        internal static bool ForceFinishFailureForTest { get; set; }
        internal static long CommandQueueCreationCountForTest
            => Interlocked.Read(ref commandQueueCreationCount);
        static long commandQueueCreationCount;
        internal static long CommandQueueReleaseCountForTest
            => Interlocked.Read(ref commandQueueReleaseCount);
        static long commandQueueReleaseCount;
#endif

        public static bool TryInitialize(out string? failureReason)
        {
            try
            {
                _ = GetApi();
                _ = SelectFirstGpuDevice();
                failureReason = null;
                return true;
            }
            catch (Exception e) when (e is OpenClException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                failureReason = e.Message;
                return false;
            }
        }

        public static nint SelectFirstGpuDevice()
        {
            var selected = Volatile.Read(ref selectedGpuDevice);
            if (selected != 0)
                return selected;
            lock (loadLock)
            {
                selected = selectedGpuDevice;
                if (selected != 0)
                    return selected;

                var devices = EnumerateGpuDevices();
                if (devices.Count == 0)
                    throw new OpenClException(DeviceNotFound, "OpenCL GPUデバイス選択", "GPUデバイスが見つかりません。");

                OpenClException? lastFailure = null;
                foreach (var candidate in devices)
                {
                    nint context = 0;
                    nint queue = 0;
                    try
                    {
                        context = CreateContext(candidate.Device);
                        queue = CreateCommandQueue(context, candidate.Device);
                        var queueToRelease = queue;
                        queue = 0;
                        ReleaseCommandQueue(queueToRelease);
                        var contextToRelease = context;
                        context = 0;
                        ReleaseContext(contextToRelease);
                        Volatile.Write(ref selectedGpuDevice, candidate.Device);
                        return candidate.Device;
                    }
                    catch (OpenClException e)
                    {
                        lastFailure = e;
                    }
                    finally
                    {
                        if (queue != 0)
                        {
                            var queueToRelease = queue;
                            queue = 0;
                            try { ReleaseCommandQueue(queueToRelease); } catch { }
                        }
                        if (context != 0)
                        {
                            var contextToRelease = context;
                            context = 0;
                            try { ReleaseContext(contextToRelease); } catch { }
                        }
                    }
                }

                throw lastFailure!;
            }
        }

        static List<(nint Platform, nint Device)> EnumerateGpuDevices()
        {
            var api = GetApi();
            var resultDevices = new List<(nint Platform, nint Device)>();
            Check(api.GetPlatformIDs(0, null, out var platformCount), nameof(api.GetPlatformIDs));
            if (platformCount == 0)
                return resultDevices;
            var platforms = new nint[checked((int)platformCount)];
            fixed (nint* platformPointer = platforms)
                Check(api.GetPlatformIDs(platformCount, platformPointer, out _), nameof(api.GetPlatformIDs));
            foreach (var platform in platforms)
            {
                var result = api.GetDeviceIDs(platform, DeviceTypeGpu, 0, null, out var deviceCount);
                if (result == DeviceNotFound || deviceCount == 0)
                    continue;
                Check(result, nameof(api.GetDeviceIDs));
                var devices = new nint[checked((int)deviceCount)];
                fixed (nint* devicePointer = devices)
                    Check(api.GetDeviceIDs(platform, DeviceTypeGpu, deviceCount, devicePointer, out _), nameof(api.GetDeviceIDs));
                foreach (var device in devices)
                    resultDevices.Add((platform, device));
            }
            return resultDevices;
        }

        public static nint CreateContext(nint device)
        {
            var api = GetApi();
            var result = Success;
            var context = api.CreateContext(null, 1, &device, 0, 0, &result);
            Check(result, nameof(api.CreateContext));
            if (context == 0)
                throw new OpenClException(OutOfHostMemory, nameof(api.CreateContext), "nullが返されました。");
            return context;
        }

        public static nint CreateD3D11Context(nint platform, nint device, nint d3d11Device)
        {
            var api = GetApi();
            var properties = stackalloc nint[]
            {
                ContextPlatform,
                platform,
                ContextD3D11Device,
                d3d11Device,
                0,
            };
            var result = Success;
            var context = api.CreateContext(properties, 1, &device, 0, 0, &result);
            Check(result, nameof(api.CreateContext));
            if (context == 0)
                throw new OpenClException(OutOfHostMemory, nameof(api.CreateContext), "D3D11共有contextにnullが返されました。");
            return context;
        }

        public static bool TrySelectD3D11Device(
            nint dxgiAdapter,
            out nint platform,
            out nint device,
            out OpenClD3D11SharingFunctions? sharing,
            out string? failureReason)
        {
            var reasons = new List<string>();
            foreach (var candidatePlatform in EnumeratePlatforms())
            {
                string extensions;
                try
                {
                    extensions = GetPlatformExtensions(candidatePlatform);
                }
                catch (OpenClException e)
                {
                    reasons.Add($"platform拡張取得: {e.Message}");
                    continue;
                }
                foreach (var kind in GetD3D11SharingPreference(extensions))
                {
                    try
                    {
                        var functions = CreateD3D11SharingFunctions(candidatePlatform, kind);
                        var candidateDevice = functions.GetPreferredDevice(dxgiAdapter);
                        platform = candidatePlatform;
                        device = candidateDevice;
                        sharing = functions;
                        failureReason = null;
                        return true;
                    }
                    catch (Exception e) when (e is OpenClException or EntryPointNotFoundException)
                    {
                        reasons.Add($"{kind}: {e.Message}");
                    }
                }
            }

            platform = 0;
            device = 0;
            sharing = null;
            failureReason = reasons.Count == 0
                ? "cl_khr_d3d11_sharing / cl_nv_d3d11_sharingを公開するplatformがありません。"
                : string.Join(" / ", reasons);
            return false;
        }

        static nint[] EnumeratePlatforms()
        {
            var api = GetApi();
            Check(api.GetPlatformIDs(0, null, out var platformCount), nameof(api.GetPlatformIDs));
            if (platformCount == 0)
                return [];
            var platforms = new nint[checked((int)platformCount)];
            fixed (nint* pointer = platforms)
                Check(api.GetPlatformIDs(platformCount, pointer, out _), nameof(api.GetPlatformIDs));
            return platforms;
        }

        static string GetPlatformExtensions(nint platform)
        {
            var api = GetApi();
            Check(api.GetPlatformInfo(platform, PlatformExtensions, 0, null, out var byteCount), nameof(api.GetPlatformInfo));
            if (byteCount == 0)
                return "";
            var bytes = new byte[checked((int)byteCount)];
            fixed (byte* pointer = bytes)
            {
                Check(api.GetPlatformInfo(platform, PlatformExtensions, byteCount, pointer, out _), nameof(api.GetPlatformInfo));
                return Marshal.PtrToStringUTF8((nint)pointer) ?? "";
            }
        }

        static IEnumerable<OpenClD3D11SharingKind> GetD3D11SharingPreference(string extensions)
        {
            var names = extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (names.Contains("cl_khr_d3d11_sharing", StringComparer.Ordinal))
                yield return OpenClD3D11SharingKind.Khr;
            if (names.Contains("cl_nv_d3d11_sharing", StringComparer.Ordinal))
                yield return OpenClD3D11SharingKind.Nv;
        }

        static OpenClD3D11SharingFunctions CreateD3D11SharingFunctions(nint platform, OpenClD3D11SharingKind kind)
        {
            var suffix = kind == OpenClD3D11SharingKind.Khr ? "KHR" : "NV";
            return new OpenClD3D11SharingFunctions(
                platform,
                kind,
                LoadExtension<GetDeviceIDsFromD3D11Delegate>(platform, $"clGetDeviceIDsFromD3D11{suffix}"),
                LoadExtension<CreateFromD3D11Texture2DDelegate>(platform, $"clCreateFromD3D11Texture2D{suffix}"),
                LoadExtension<EnqueueD3D11ObjectsDelegate>(platform, $"clEnqueueAcquireD3D11Objects{suffix}"),
                LoadExtension<EnqueueD3D11ObjectsDelegate>(platform, $"clEnqueueReleaseD3D11Objects{suffix}"));
        }

        static T LoadExtension<T>(nint platform, string name) where T : Delegate
        {
            var api = GetApi();
            var namePointer = Marshal.StringToCoTaskMemUTF8(name);
            try
            {
                var address = api.GetExtensionFunctionAddressForPlatform is not null
                    ? api.GetExtensionFunctionAddressForPlatform(platform, (byte*)namePointer)
                    : api.GetExtensionFunctionAddress is not null
                        ? api.GetExtensionFunctionAddress((byte*)namePointer)
                        : 0;
                if (address == 0)
                    throw new EntryPointNotFoundException(name);
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
            finally
            {
                Marshal.FreeCoTaskMem(namePointer);
            }
        }

        public static void ReleaseContext(nint context)
            => Check(GetApi().ReleaseContext(context), nameof(Api.ReleaseContext));

        public static void RetainContext(nint context)
            => Check(GetApi().RetainContext(context), nameof(Api.RetainContext));

        public static nint CreateCommandQueue(nint context, nint device)
        {
            var api = GetApi();
            var result = Success;
            var queue = api.CreateCommandQueue(context, device, 0, &result);
            Check(result, nameof(api.CreateCommandQueue));
            if (queue == 0)
                throw new OpenClException(OutOfHostMemory, nameof(api.CreateCommandQueue), "nullが返されました。");
#if DEBUG
            Interlocked.Increment(ref commandQueueCreationCount);
#endif
            return queue;
        }

        public static void ReleaseCommandQueue(nint queue)
        {
            Check(GetApi().ReleaseCommandQueue(queue), nameof(Api.ReleaseCommandQueue));
#if DEBUG
            Interlocked.Increment(ref commandQueueReleaseCount);
#endif
        }

        public static nint CreateBuffer(nint context, nuint byteCount)
        {
            var api = GetApi();
            var result = Success;
            var buffer = api.CreateBuffer(context, MemReadWrite, byteCount, 0, &result);
            Check(result, nameof(api.CreateBuffer));
            return buffer;
        }

        public static void ReleaseBuffer(nint buffer)
            => Check(GetApi().ReleaseMemObject(buffer), nameof(Api.ReleaseMemObject));

        public static void WriteBuffer(nint queue, nint buffer, nint source, nuint byteCount)
            => WriteBuffer(queue, buffer, source, 0, byteCount);

        static void WriteBuffer(nint queue, nint buffer, nint source, nuint offset, nuint byteCount)
            => Check(GetApi().EnqueueWriteBuffer(queue, buffer, 1, offset, byteCount, source, 0, null, null), nameof(Api.EnqueueWriteBuffer));

        public static void ReadBuffer(nint queue, nint buffer, nint destination, nuint byteCount)
            => Check(GetApi().EnqueueReadBuffer(queue, buffer, 1, 0, byteCount, destination, 0, null, null), nameof(Api.EnqueueReadBuffer));

        public static void ZeroBuffer(nint queue, nint buffer, nuint byteCount)
        {
            const nuint ChunkSize = 16 * 1024 * 1024;
            var allocationSize = nuint.Min(byteCount, ChunkSize);
            var zero = NativeMemory.AllocZeroed(allocationSize);
            try
            {
                for (nuint offset = 0; offset < byteCount;)
                {
                    var size = nuint.Min(allocationSize, byteCount - offset);
                    WriteBuffer(queue, buffer, (nint)zero, offset, size);
                    offset += size;
                }
            }
            finally
            {
                NativeMemory.Free(zero);
            }
        }

        public static void Finish(nint queue)
        {
#if DEBUG
            if (ForceFinishFailureForTest)
                throw new OpenClException(OutOfResources, "テスト用clFinish失敗", "");
#endif
            Check(GetApi().Finish(queue), nameof(Api.Finish));
        }

        public static nint CompileProgram(nint context, nint device, string source)
        {
            var api = GetApi();
            var sourcePointer = Marshal.StringToCoTaskMemUTF8(source);
            try
            {
                var result = Success;
                var program = api.CreateProgramWithSource(context, 1, &sourcePointer, null, &result);
                Check(result, nameof(api.CreateProgramWithSource));
                result = api.BuildProgram(program, 1, &device, null, 0, 0);
                if (result == Success)
                    return program;
                var buildLog = GetProgramBuildLog(api, program, device);
                try
                {
                    api.ReleaseProgram(program);
                }
                catch
                {
                }
                throw new OpenClException(result, nameof(api.BuildProgram), buildLog);
            }
            finally
            {
                Marshal.FreeCoTaskMem(sourcePointer);
            }
        }

        public static void ReleaseProgram(nint program)
            => Check(GetApi().ReleaseProgram(program), nameof(Api.ReleaseProgram));

        public static nint CreateKernel(nint program, string name)
        {
            var api = GetApi();
            var namePointer = Marshal.StringToCoTaskMemUTF8(name);
            try
            {
                var result = Success;
                var kernel = api.CreateKernel(program, (byte*)namePointer, &result);
                Check(result, nameof(api.CreateKernel));
                return kernel;
            }
            finally
            {
                Marshal.FreeCoTaskMem(namePointer);
            }
        }

        public static void ReleaseKernel(nint kernel)
            => Check(GetApi().ReleaseKernel(kernel), nameof(Api.ReleaseKernel));

        public static void SetKernelArgument(nint kernel, uint index, nint value)
            => Check(GetApi().SetKernelArg(kernel, index, (nuint)sizeof(nint), &value), nameof(Api.SetKernelArg));

        public static void SetKernelArgument(nint kernel, uint index, int value)
            => Check(GetApi().SetKernelArg(kernel, index, (nuint)sizeof(int), &value), nameof(Api.SetKernelArg));

        public static void EnqueueKernel2D(nint queue, nint kernel, int width, int height)
        {
            var globalSize = stackalloc nuint[] { (nuint)width, (nuint)height };
            Check(GetApi().EnqueueNDRangeKernel(queue, kernel, 2, null, globalSize, null, 0, null, null), nameof(Api.EnqueueNDRangeKernel));
        }

        static string GetProgramBuildLog(Api api, nint program, nint device)
        {
            if (api.GetProgramBuildInfo(program, device, ProgramBuildLog, 0, null, out var size) != Success || size == 0)
                return "";
            var buffer = new byte[checked((int)size)];
            fixed (byte* pointer = buffer)
            {
                if (api.GetProgramBuildInfo(program, device, ProgramBuildLog, size, pointer, out _) != Success)
                    return "";
                return Marshal.PtrToStringUTF8((nint)pointer) ?? "";
            }
        }

        static Api GetApi()
        {
            var current = Volatile.Read(ref api);
            if (current is not null)
                return current;
            lock (loadLock)
            {
                current = api;
                if (current is not null)
                    return current;
                if (!NativeLibrary.TryLoad(LibraryName, out var library))
                    throw new DllNotFoundException($"{LibraryName} を読み込めません。");
                current = new Api(library);
                Volatile.Write(ref api, current);
                return current;
            }
        }

        static void Check(int result, string operation)
        {
            if (result != Success)
                throw new OpenClException(result, operation, "");
        }

        sealed class Api
        {
            public readonly GetPlatformIDsDelegate GetPlatformIDs;
            public readonly GetPlatformInfoDelegate GetPlatformInfo;
            public readonly GetDeviceIDsDelegate GetDeviceIDs;
            public readonly GetExtensionFunctionAddressForPlatformDelegate? GetExtensionFunctionAddressForPlatform;
            public readonly GetExtensionFunctionAddressDelegate? GetExtensionFunctionAddress;
            public readonly CreateContextDelegate CreateContext;
            public readonly ReleaseHandleDelegate RetainContext;
            public readonly ReleaseHandleDelegate ReleaseContext;
            public readonly CreateCommandQueueDelegate CreateCommandQueue;
            public readonly ReleaseHandleDelegate ReleaseCommandQueue;
            public readonly CreateBufferDelegate CreateBuffer;
            public readonly ReleaseHandleDelegate ReleaseMemObject;
            public readonly EnqueueBufferDelegate EnqueueWriteBuffer;
            public readonly EnqueueBufferDelegate EnqueueReadBuffer;
            public readonly ReleaseHandleDelegate Finish;
            public readonly CreateProgramWithSourceDelegate CreateProgramWithSource;
            public readonly BuildProgramDelegate BuildProgram;
            public readonly GetProgramBuildInfoDelegate GetProgramBuildInfo;
            public readonly ReleaseHandleDelegate ReleaseProgram;
            public readonly CreateKernelDelegate CreateKernel;
            public readonly SetKernelArgDelegate SetKernelArg;
            public readonly EnqueueNDRangeKernelDelegate EnqueueNDRangeKernel;
            public readonly ReleaseHandleDelegate ReleaseKernel;

            public Api(nint module)
            {
                GetPlatformIDs = Load<GetPlatformIDsDelegate>(module, "clGetPlatformIDs");
                GetPlatformInfo = Load<GetPlatformInfoDelegate>(module, "clGetPlatformInfo");
                GetDeviceIDs = Load<GetDeviceIDsDelegate>(module, "clGetDeviceIDs");
                GetExtensionFunctionAddressForPlatform = TryLoad<GetExtensionFunctionAddressForPlatformDelegate>(
                    module,
                    "clGetExtensionFunctionAddressForPlatform");
                GetExtensionFunctionAddress = TryLoad<GetExtensionFunctionAddressDelegate>(
                    module,
                    "clGetExtensionFunctionAddress");
                CreateContext = Load<CreateContextDelegate>(module, "clCreateContext");
                RetainContext = Load<ReleaseHandleDelegate>(module, "clRetainContext");
                ReleaseContext = Load<ReleaseHandleDelegate>(module, "clReleaseContext");
                CreateCommandQueue = Load<CreateCommandQueueDelegate>(module, "clCreateCommandQueue");
                ReleaseCommandQueue = Load<ReleaseHandleDelegate>(module, "clReleaseCommandQueue");
                CreateBuffer = Load<CreateBufferDelegate>(module, "clCreateBuffer");
                ReleaseMemObject = Load<ReleaseHandleDelegate>(module, "clReleaseMemObject");
                EnqueueWriteBuffer = Load<EnqueueBufferDelegate>(module, "clEnqueueWriteBuffer");
                EnqueueReadBuffer = Load<EnqueueBufferDelegate>(module, "clEnqueueReadBuffer");
                Finish = Load<ReleaseHandleDelegate>(module, "clFinish");
                CreateProgramWithSource = Load<CreateProgramWithSourceDelegate>(module, "clCreateProgramWithSource");
                BuildProgram = Load<BuildProgramDelegate>(module, "clBuildProgram");
                GetProgramBuildInfo = Load<GetProgramBuildInfoDelegate>(module, "clGetProgramBuildInfo");
                ReleaseProgram = Load<ReleaseHandleDelegate>(module, "clReleaseProgram");
                CreateKernel = Load<CreateKernelDelegate>(module, "clCreateKernel");
                SetKernelArg = Load<SetKernelArgDelegate>(module, "clSetKernelArg");
                EnqueueNDRangeKernel = Load<EnqueueNDRangeKernelDelegate>(module, "clEnqueueNDRangeKernel");
                ReleaseKernel = Load<ReleaseHandleDelegate>(module, "clReleaseKernel");
            }

            static T Load<T>(nint module, string name) where T : Delegate
                => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));

            static T? TryLoad<T>(nint module, string name) where T : Delegate
                => NativeLibrary.TryGetExport(module, name, out var address)
                    ? Marshal.GetDelegateForFunctionPointer<T>(address)
                    : null;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int GetPlatformIDsDelegate(uint count, nint* platforms, out uint returnedCount);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int GetPlatformInfoDelegate(nint platform, uint parameter, nuint size, void* value, out nuint returnedSize);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int GetDeviceIDsDelegate(nint platform, ulong type, uint count, nint* devices, out uint returnedCount);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint GetExtensionFunctionAddressForPlatformDelegate(nint platform, byte* name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint GetExtensionFunctionAddressDelegate(byte* name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint CreateContextDelegate(nint* properties, uint count, nint* devices, nint callback, nint userData, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int ReleaseHandleDelegate(nint handle);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint CreateCommandQueueDelegate(nint context, nint device, ulong properties, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint CreateBufferDelegate(nint context, ulong flags, nuint size, nint hostPointer, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int EnqueueBufferDelegate(nint queue, nint buffer, uint blocking, nuint offset, nuint size, nint pointer, uint waitCount, nint* waitList, nint* resultEvent);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint CreateProgramWithSourceDelegate(nint context, uint count, nint* strings, nuint* lengths, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int BuildProgramDelegate(nint program, uint count, nint* devices, byte* options, nint callback, nint userData);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int GetProgramBuildInfoDelegate(nint program, nint device, uint parameter, nuint size, void* value, out nuint returnedSize);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint CreateKernelDelegate(nint program, byte* name, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int SetKernelArgDelegate(nint kernel, uint index, nuint size, void* value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int EnqueueNDRangeKernelDelegate(nint queue, nint kernel, uint dimensions, nuint* globalOffset, nuint* globalSize, nuint* localSize, uint waitCount, nint* waitList, nint* resultEvent);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int GetDeviceIDsFromD3D11Delegate(nint platform, uint source, nint d3dObject, uint set, uint count, nint* devices, out uint returnedCount);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint CreateFromD3D11Texture2DDelegate(nint context, ulong flags, nint resource, uint subresource, int* result);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int EnqueueD3D11ObjectsDelegate(nint queue, uint count, nint* memoryObjects, uint waitCount, nint* waitList, nint* resultEvent);

        internal sealed class OpenClD3D11SharingFunctions
        {
            readonly GetDeviceIDsFromD3D11Delegate getDeviceIDs;
            readonly CreateFromD3D11Texture2DDelegate createTexture;
            readonly EnqueueD3D11ObjectsDelegate acquireObjects;
            readonly EnqueueD3D11ObjectsDelegate releaseObjects;

            public nint Platform { get; }
            public OpenClD3D11SharingKind Kind { get; }

            internal OpenClD3D11SharingFunctions(
                nint platform,
                OpenClD3D11SharingKind kind,
                GetDeviceIDsFromD3D11Delegate getDeviceIDs,
                CreateFromD3D11Texture2DDelegate createTexture,
                EnqueueD3D11ObjectsDelegate acquireObjects,
                EnqueueD3D11ObjectsDelegate releaseObjects)
            {
                Platform = platform;
                Kind = kind;
                this.getDeviceIDs = getDeviceIDs;
                this.createTexture = createTexture;
                this.acquireObjects = acquireObjects;
                this.releaseObjects = releaseObjects;
            }

            public nint GetPreferredDevice(nint dxgiAdapter)
            {
                var result = getDeviceIDs(
                    Platform,
                    D3D11DxgiAdapter,
                    dxgiAdapter,
                    PreferredDevicesForD3D11,
                    0,
                    null,
                    out var count);
                if (result == DeviceNotFound || count == 0)
                    throw new OpenClException(DeviceNotFound, nameof(getDeviceIDs), "対応デバイスがありません。");
                Check(result, nameof(getDeviceIDs));
                var devices = new nint[checked((int)count)];
                fixed (nint* pointer = devices)
                    Check(getDeviceIDs(
                        Platform,
                        D3D11DxgiAdapter,
                        dxgiAdapter,
                        PreferredDevicesForD3D11,
                        count,
                        pointer,
                        out _), nameof(getDeviceIDs));
                return devices[0];
            }

            public nint CreateTexture(nint context, nint d3d11Texture)
            {
                var result = Success;
                var image = createTexture(context, MemReadWrite, d3d11Texture, 0, &result);
                Check(result, nameof(createTexture));
                if (image == 0)
                    throw new OpenClException(OutOfHostMemory, nameof(createTexture), "nullが返されました。");
                return image;
            }

            public void Acquire(nint queue, nint image)
                => Check(acquireObjects(queue, 1, &image, 0, null, null), nameof(acquireObjects));

            public void Release(nint queue, nint image)
                => Check(releaseObjects(queue, 1, &image, 0, null, null), nameof(releaseObjects));
        }
    }

    internal enum OpenClD3D11SharingKind
    {
        Khr,
        Nv,
    }

    internal sealed class OpenClException : Exception
    {
        public int Result { get; }
        public int FallbackStatus => Result is OpenClDriver.MemObjectAllocationFailure or OpenClDriver.OutOfHostMemory or OpenClDriver.OutOfResources
            ? OfxStatus.GPUOutOfMemory
            : OfxStatus.GPURenderFailed;

        public OpenClException(int result, string operation, string detail)
            : base($"{operation} が失敗しました。OpenCL result={result}{(string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail})")}")
        {
            Result = result;
        }
    }
}
