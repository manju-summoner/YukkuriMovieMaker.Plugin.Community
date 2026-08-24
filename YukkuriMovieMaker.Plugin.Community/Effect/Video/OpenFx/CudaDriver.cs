using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// CUDA Driver API（nvcuda.dll）の最小P/Invoke層。
    /// CUDA Toolkitには依存せず、Windowsへインストール済みのNVIDIAドライバーだけを使用する。
    /// </summary>
    internal static unsafe class CudaDriver
    {
        const string LibraryName = "nvcuda.dll";

        internal const int Success = 0;
        internal const int ErrorOutOfMemory = 2;

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuInit(uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuDeviceGetCount(out int count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuDeviceGet(out int device, int ordinal);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuDevicePrimaryCtxRetain(out nint context, int device);

        [DllImport(LibraryName, EntryPoint = "cuDevicePrimaryCtxRelease_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuDevicePrimaryCtxRelease(int device);

        [DllImport(LibraryName, EntryPoint = "cuCtxPushCurrent_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuCtxPushCurrent(nint context);

        [DllImport(LibraryName, EntryPoint = "cuCtxPopCurrent_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuCtxPopCurrent(out nint context);

        [DllImport(LibraryName, EntryPoint = "cuMemAlloc_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemAlloc(out ulong devicePointer, nuint byteCount);

        [DllImport(LibraryName, EntryPoint = "cuMemFree_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemFree(ulong devicePointer);

        [DllImport(LibraryName, EntryPoint = "cuMemsetD8_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemsetD8(ulong destination, byte value, nuint byteCount);

        [DllImport(LibraryName, EntryPoint = "cuMemcpyHtoD_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemcpyHtoD(ulong destination, nint source, nuint byteCount);

        [DllImport(LibraryName, EntryPoint = "cuMemcpyDtoH_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemcpyDtoH(nint destination, ulong source, nuint byteCount);

        [DllImport(LibraryName, EntryPoint = "cuMemcpy2DAsync_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuMemcpy2DAsync(in CudaMemcpy2D copy, nint stream);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuD3D11GetDevice(out int device, nint adapter);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGraphicsD3D11RegisterResource(out nint resource, nint d3d11Resource, uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGraphicsUnregisterResource(nint resource);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGraphicsMapResources(uint count, nint* resources, nint stream);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGraphicsUnmapResources(uint count, nint* resources, nint stream);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGraphicsSubResourceGetMappedArray(out nint array, nint resource, uint arrayIndex, uint mipLevel);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuStreamCreate(out nint stream, uint flags);

        [DllImport(LibraryName, EntryPoint = "cuStreamDestroy_v2", CallingConvention = CallingConvention.Winapi)]
        static extern int cuStreamDestroy(nint stream);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuStreamSynchronize(nint stream);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuModuleLoadData(out nint module, nint image);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuModuleGetFunction(out nint function, nint module, [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuModuleUnload(nint module);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuLaunchKernel(
            nint function,
            uint gridDimX,
            uint gridDimY,
            uint gridDimZ,
            uint blockDimX,
            uint blockDimY,
            uint blockDimZ,
            uint sharedMemoryBytes,
            nint stream,
            void** kernelParameters,
            void** extra);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGetErrorName(int error, out nint name);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Winapi)]
        static extern int cuGetErrorString(int error, out nint description);

        public static bool TryInitialize(out string? failureReason)
        {
            try
            {
                Check(cuInit(0), nameof(cuInit));
                Check(cuDeviceGetCount(out var count), nameof(cuDeviceGetCount));
                if (count <= 0)
                {
                    failureReason = "CUDAデバイスが見つかりません。";
                    return false;
                }
                failureReason = null;
                return true;
            }
            catch (Exception e) when (e is CudaException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                failureReason = e.Message;
                return false;
            }
        }

        public static int GetDevice(int ordinal)
        {
            Check(cuDeviceGet(out var device, ordinal), nameof(cuDeviceGet));
            return device;
        }

        public static nint RetainPrimaryContext(int device)
        {
            Check(cuDevicePrimaryCtxRetain(out var context, device), nameof(cuDevicePrimaryCtxRetain));
            return context;
        }

        public static void ReleasePrimaryContext(int device)
            => Check(cuDevicePrimaryCtxRelease(device), nameof(cuDevicePrimaryCtxRelease));

        public static void PushContext(nint context)
            => Check(cuCtxPushCurrent(context), nameof(cuCtxPushCurrent));

        public static void PopContext()
            => Check(cuCtxPopCurrent(out _), nameof(cuCtxPopCurrent));

        public static ulong Allocate(nuint byteCount)
        {
            Check(cuMemAlloc(out var pointer, byteCount), nameof(cuMemAlloc));
            return pointer;
        }

        public static void Free(ulong pointer)
            => Check(cuMemFree(pointer), nameof(cuMemFree));

        public static void MemsetD8(ulong destination, byte value, nuint byteCount)
            => Check(cuMemsetD8(destination, value, byteCount), nameof(cuMemsetD8));

        public static void CopyHostToDevice(ulong destination, nint source, nuint byteCount)
            => Check(cuMemcpyHtoD(destination, source, byteCount), nameof(cuMemcpyHtoD));

        public static void CopyDeviceToHost(nint destination, ulong source, nuint byteCount)
            => Check(cuMemcpyDtoH(destination, source, byteCount), nameof(cuMemcpyDtoH));

        public static int GetD3D11Device(nint adapter)
        {
            Check(cuD3D11GetDevice(out var device, adapter), nameof(cuD3D11GetDevice));
            return device;
        }

        public static nint RegisterD3D11Resource(nint resource)
        {
            Check(cuGraphicsD3D11RegisterResource(out var graphicsResource, resource, 0), nameof(cuGraphicsD3D11RegisterResource));
            return graphicsResource;
        }

        public static void UnregisterGraphicsResource(nint resource)
            => Check(cuGraphicsUnregisterResource(resource), nameof(cuGraphicsUnregisterResource));

        public static void MapGraphicsResource(nint resource, nint stream)
            => Check(cuGraphicsMapResources(1, &resource, stream), nameof(cuGraphicsMapResources));

        public static void UnmapGraphicsResource(nint resource, nint stream)
            => Check(cuGraphicsUnmapResources(1, &resource, stream), nameof(cuGraphicsUnmapResources));

        public static nint GetMappedArray(nint resource)
        {
            Check(cuGraphicsSubResourceGetMappedArray(out var array, resource, 0, 0), nameof(cuGraphicsSubResourceGetMappedArray));
            return array;
        }

        public static void CopyArrayToDeviceAsync(nint sourceArray, ulong destination, nuint destinationPitch, nuint widthBytes, nuint height, nint stream)
        {
            var copy = new CudaMemcpy2D
            {
                SourceMemoryType = CudaMemoryType.Array,
                SourceArray = sourceArray,
                DestinationMemoryType = CudaMemoryType.Device,
                DestinationDevice = destination,
                DestinationPitch = destinationPitch,
                WidthInBytes = widthBytes,
                Height = height,
            };
            Check(cuMemcpy2DAsync(in copy, stream), nameof(cuMemcpy2DAsync));
        }

        public static void CopyDeviceToArrayAsync(ulong source, nuint sourcePitch, nint destinationArray, nuint widthBytes, nuint height, nint stream)
        {
            var copy = new CudaMemcpy2D
            {
                SourceMemoryType = CudaMemoryType.Device,
                SourceDevice = source,
                SourcePitch = sourcePitch,
                DestinationMemoryType = CudaMemoryType.Array,
                DestinationArray = destinationArray,
                WidthInBytes = widthBytes,
                Height = height,
            };
            Check(cuMemcpy2DAsync(in copy, stream), nameof(cuMemcpy2DAsync));
        }

        public static nint CreateStream()
        {
            Check(cuStreamCreate(out var stream, 0), nameof(cuStreamCreate));
            return stream;
        }

        public static void DestroyStream(nint stream)
            => Check(cuStreamDestroy(stream), nameof(cuStreamDestroy));

        public static void SynchronizeStream(nint stream)
            => Check(cuStreamSynchronize(stream), nameof(cuStreamSynchronize));

        public static nint LoadModule(string ptx)
        {
            var image = Marshal.StringToCoTaskMemUTF8(ptx);
            try
            {
                Check(cuModuleLoadData(out var module, image), nameof(cuModuleLoadData));
                return module;
            }
            finally
            {
                Marshal.FreeCoTaskMem(image);
            }
        }

        public static nint GetFunction(nint module, string name)
        {
            Check(cuModuleGetFunction(out var function, module, name), nameof(cuModuleGetFunction));
            return function;
        }

        public static void UnloadModule(nint module)
            => Check(cuModuleUnload(module), nameof(cuModuleUnload));

        public static void LaunchKernel(
            nint function,
            uint gridDimX,
            uint blockDimX,
            nint stream,
            void** kernelParameters)
            => Check(
                cuLaunchKernel(function, gridDimX, 1, 1, blockDimX, 1, 1, 0, stream, kernelParameters, null),
                nameof(cuLaunchKernel));

        static void Check(int result, string operation)
        {
            if (result == Success)
                return;
            throw new CudaException(result, operation, GetErrorText(result));
        }

        static string GetErrorText(int result)
        {
            var name = cuGetErrorName(result, out var namePointer) == Success
                ? Marshal.PtrToStringAnsi(namePointer)
                : null;
            var description = cuGetErrorString(result, out var descriptionPointer) == Success
                ? Marshal.PtrToStringAnsi(descriptionPointer)
                : null;
            return string.Join(": ", new[] { name, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        enum CudaMemoryType
        {
            Host = 1,
            Device = 2,
            Array = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CudaMemcpy2D
        {
            public nuint SourceXInBytes;
            public nuint SourceY;
            public CudaMemoryType SourceMemoryType;
            public nint SourceHost;
            public ulong SourceDevice;
            public nint SourceArray;
            public nuint SourcePitch;
            public nuint DestinationXInBytes;
            public nuint DestinationY;
            public CudaMemoryType DestinationMemoryType;
            public nint DestinationHost;
            public ulong DestinationDevice;
            public nint DestinationArray;
            public nuint DestinationPitch;
            public nuint WidthInBytes;
            public nuint Height;
        }
    }

    /// <summary>CUDA Driver API呼び出しの失敗。</summary>
    internal sealed class CudaException : Exception
    {
        public int Result { get; }
        public int FallbackStatus => Result == CudaDriver.ErrorOutOfMemory
            ? OfxStatus.GPUOutOfMemory
            : OfxStatus.GPURenderFailed;

        public CudaException(int result, string operation, string errorText)
            : base($"{operation} が失敗しました。CUDA result={result}{(string.IsNullOrEmpty(errorText) ? "" : $" ({errorText})")}")
        {
            Result = result;
        }
    }
}
