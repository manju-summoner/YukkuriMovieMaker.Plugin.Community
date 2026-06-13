using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Interop;

[ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    void GetItem(in Guid guidKey, nint pValue);
    void GetItemType(in Guid guidKey, out int pType);
    void CompareItem(in Guid guidKey, nint value, out int pbResult);
    void Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    void GetUINT32(in Guid guidKey, out uint punValue);
    void GetUINT64(in Guid guidKey, out ulong punValue);
    void GetDouble(in Guid guidKey, out double pfValue);
    void GetGUID(in Guid guidKey, out Guid pguidValue);
    void GetStringLength(in Guid guidKey, out uint pcchLength);
    void GetString(in Guid guidKey, nint pwszValue, uint cchBufSize, nint pcchLength);
    void GetAllocatedString(in Guid guidKey, out nint ppwszValue, out uint pcchLength);
    void GetBlobSize(in Guid guidKey, out uint pcbBlobSize);
    void GetBlob(in Guid guidKey, nint pBuf, uint cbBufSize, nint pcbSize);
    void GetAllocatedBlob(in Guid guidKey, out nint ppBuf, out uint pcbSize);
    void GetUnknown(in Guid guidKey, in Guid riid, out nint ppv);
    void SetItem(in Guid guidKey, nint value);
    void DeleteItem(in Guid guidKey);
    void DeleteAllItems();
    void SetUINT32(in Guid guidKey, uint unValue);
    void SetUINT64(in Guid guidKey, ulong unValue);
    void SetDouble(in Guid guidKey, double fValue);
    void SetGUID(in Guid guidKey, in Guid guidValue);
    void SetString(in Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    void SetBlob(in Guid guidKey, nint pBuf, uint cbBufSize);
    void SetUnknown(in Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    void LockStore();
    void UnlockStore();
    void GetCount(out uint pcItems);
    void GetItemByIndex(uint unIndex, out Guid pguidKey, nint pValue);
    void CopyAllItems(IMFAttributes pDest);
}

[ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType
{
    void GetItem(in Guid guidKey, nint pValue);
    void GetItemType(in Guid guidKey, out int pType);
    void CompareItem(in Guid guidKey, nint value, out int pbResult);
    void Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    void GetUINT32(in Guid guidKey, out uint punValue);
    void GetUINT64(in Guid guidKey, out ulong punValue);
    void GetDouble(in Guid guidKey, out double pfValue);
    void GetGUID(in Guid guidKey, out Guid pguidValue);
    void GetStringLength(in Guid guidKey, out uint pcchLength);
    void GetString(in Guid guidKey, nint pwszValue, uint cchBufSize, nint pcchLength);
    void GetAllocatedString(in Guid guidKey, out nint ppwszValue, out uint pcchLength);
    void GetBlobSize(in Guid guidKey, out uint pcbBlobSize);
    void GetBlob(in Guid guidKey, nint pBuf, uint cbBufSize, nint pcbSize);
    void GetAllocatedBlob(in Guid guidKey, out nint ppBuf, out uint pcbSize);
    void GetUnknown(in Guid guidKey, in Guid riid, out nint ppv);
    void SetItem(in Guid guidKey, nint value);
    void DeleteItem(in Guid guidKey);
    void DeleteAllItems();
    void SetUINT32(in Guid guidKey, uint unValue);
    void SetUINT64(in Guid guidKey, ulong unValue);
    void SetDouble(in Guid guidKey, double fValue);
    void SetGUID(in Guid guidKey, in Guid guidValue);
    void SetString(in Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    void SetBlob(in Guid guidKey, nint pBuf, uint cbBufSize);
    void SetUnknown(in Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    void LockStore();
    void UnlockStore();
    void GetCount(out uint pcItems);
    void GetItemByIndex(uint unIndex, out Guid pguidKey, nint pValue);
    void CopyAllItems(IMFAttributes pDest);
    void GetMajorType(out Guid pguidMajorType);
    void IsCompressedFormat(out int pfCompressed);
    void IsEqual(IMFMediaType pIMediaType, out uint pdwFlags);
    void GetRepresentation(Guid guidRepresentation, out nint ppvRepresentation);
    void FreeRepresentation(Guid guidRepresentation, nint pvRepresentation);
}

[ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample
{
    void GetItem(in Guid guidKey, nint pValue);
    void GetItemType(in Guid guidKey, out int pType);
    void CompareItem(in Guid guidKey, nint value, out int pbResult);
    void Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    void GetUINT32(in Guid guidKey, out uint punValue);
    void GetUINT64(in Guid guidKey, out ulong punValue);
    void GetDouble(in Guid guidKey, out double pfValue);
    void GetGUID(in Guid guidKey, out Guid pguidValue);
    void GetStringLength(in Guid guidKey, out uint pcchLength);
    void GetString(in Guid guidKey, nint pwszValue, uint cchBufSize, nint pcchLength);
    void GetAllocatedString(in Guid guidKey, out nint ppwszValue, out uint pcchLength);
    void GetBlobSize(in Guid guidKey, out uint pcbBlobSize);
    void GetBlob(in Guid guidKey, nint pBuf, uint cbBufSize, nint pcbSize);
    void GetAllocatedBlob(in Guid guidKey, out nint ppBuf, out uint pcbSize);
    void GetUnknown(in Guid guidKey, in Guid riid, out nint ppv);
    void SetItem(in Guid guidKey, nint value);
    void DeleteItem(in Guid guidKey);
    void DeleteAllItems();
    void SetUINT32(in Guid guidKey, uint unValue);
    void SetUINT64(in Guid guidKey, ulong unValue);
    void SetDouble(in Guid guidKey, double fValue);
    void SetGUID(in Guid guidKey, in Guid guidValue);
    void SetString(in Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    void SetBlob(in Guid guidKey, nint pBuf, uint cbBufSize);
    void SetUnknown(in Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    void LockStore();
    void UnlockStore();
    void GetCount(out uint pcItems);
    void GetItemByIndex(uint unIndex, out Guid pguidKey, nint pValue);
    void CopyAllItems(IMFAttributes pDest);
    void GetSampleFlags(out uint pdwSampleFlags);
    void SetSampleFlags(uint dwSampleFlags);
    void GetSampleTime(out long phnsSampleTime);
    void SetSampleTime(long hnsSampleTime);
    void GetSampleDuration(out long phnsSampleDuration);
    void SetSampleDuration(long hnsSampleDuration);
    void GetBufferCount(out uint pdwBufferCount);
    void GetBufferByIndex(uint dwIndex, out IMFMediaBuffer ppBuffer);
    void ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
    void AddBuffer(IMFMediaBuffer pBuffer);
    void RemoveBufferByIndex(uint dwIndex);
    void RemoveAllBuffers();
    void GetTotalLength(out uint pcbTotalLength);
    void CopyToBuffer(IMFMediaBuffer pBuffer);
}

[ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaBuffer
{
    void Lock(out nint ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
    void Unlock();
    void GetCurrentLength(out uint pcbCurrentLength);
    void SetCurrentLength(uint cbCurrentLength);
    void GetMaxLength(out uint pcbMaxLength);
}

[ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSinkWriter
{
    void AddStream(IMFMediaType pTargetMediaType, out uint pdwStreamIndex);
    void SetInputMediaType(uint dwStreamIndex, IMFMediaType pInputMediaType, IMFAttributes? pEncodingParameters);
    void BeginWriting();
    void WriteSample(uint dwStreamIndex, IMFSample pSample);
    void SendStreamTick(uint dwStreamIndex, long llTimestamp);
    void PlaceMarker(uint dwStreamIndex, nint pvContext);
    void NotifyEndOfSegment(uint dwStreamIndex);
    void Flush(uint dwStreamIndex);
    void DoFinalize();
    void GetServiceForStream(uint dwStreamIndex, in Guid guidService, in Guid riid, out nint ppvObject);
    void GetStatistics(uint dwStreamIndex, nint pStats);
}

[ComImport, Guid("eb533d5d-2db6-40f8-97a9-494692014f07"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFDXGIDeviceManager
{
    void CloseDeviceHandle(nint hDevice);
    void GetVideoService(nint hDevice, in Guid riid, out nint ppService);
    void LockDevice(nint hDevice, in Guid riid, out nint ppUnkDevice, int fBlock);
    void OpenDeviceHandle(out nint phDevice);
    void ResetDevice(nint pUnkDevice, uint resetToken);
    void TestDevice(nint hDevice);
    void UnlockDevice(nint hDevice, int fSaveState);
}

[ComImport, Guid("9b7e4e00-342c-4106-a19f-4f2704f689f0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D10Multithread
{
    void Enter();
    void Leave();
    [PreserveSig]
    int SetMultithreadProtected(int bMTProtect);
    [PreserveSig]
    int GetMultithreadProtected();
}

[ComImport, Guid("ac6b7889-0740-4d51-8619-905994a55cc6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAsyncResult
{
    void GetState([MarshalAs(UnmanagedType.IUnknown)] out object ppunkState);
    [PreserveSig]
    int GetStatus();
    void SetStatus(int hrStatus);
    void GetObject([MarshalAs(UnmanagedType.IUnknown)] out object ppObject);
    [PreserveSig]
    nint GetStateNoAddRef();
}

[ComImport, Guid("a27003cf-2354-4f2a-8d6a-ab7cff15437e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAsyncCallback
{
    [PreserveSig]
    int GetParameters(out uint pdwFlags, out uint pdwQueue);
    [PreserveSig]
    int Invoke(IMFAsyncResult pAsyncResult);
}

[ComImport, Guid("245bf8e9-0755-40f7-88a5-ae0f18d55e17"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFTrackedSample
{
    void SetAllocator(IMFAsyncCallback pSampleAllocator, [MarshalAs(UnmanagedType.IUnknown)] object? pUnkState);
}

internal static class MediaFoundationApi
{
    private const uint MF_VERSION = 0x00020070;
    private const uint MFSTARTUP_FULL = 0;

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFStartup(uint version, uint dwFlags);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, uint cInitialSize);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateSample(out IMFSample ppIMFSample);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateTrackedSample(out IMFSample ppIMFSample);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IMFMediaBuffer ppBuffer);

    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int MFCreateSinkWriterFromURL(string pwszOutputURL, nint pByteStream, IMFAttributes? pAttributes, out IMFSinkWriter ppSinkWriter);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateDXGIDeviceManager(out uint resetToken, out IMFDXGIDeviceManager ppDeviceManager);

    [DllImport("mfplat.dll", PreserveSig = true)]
    private static extern int MFCreateDXGISurfaceBuffer(in Guid riid, nint punkSurface, uint uSubresourceIndex, int fBottomUpWhenLinear, out IMFMediaBuffer ppBuffer);

    [DllImport("d3d11.dll", PreserveSig = true)]
    private static extern int D3D11CreateDevice(nint pAdapter, int driverType, nint software, uint flags, nint pFeatureLevels, uint featureLevels, uint sdkVersion, out nint ppDevice, out int pFeatureLevel, out nint ppImmediateContext);

    public static void Startup() => Marshal.ThrowExceptionForHR(MFStartup(MF_VERSION, MFSTARTUP_FULL));

    public static void Shutdown() => Marshal.ThrowExceptionForHR(MFShutdown());

    public static IMFAttributes CreateAttributes(uint initialSize)
    {
        Marshal.ThrowExceptionForHR(MFCreateAttributes(out var attributes, initialSize));
        return attributes;
    }

    public static IMFMediaType CreateMediaType()
    {
        Marshal.ThrowExceptionForHR(MFCreateMediaType(out var type));
        return type;
    }

    public static IMFSample CreateSample()
    {
        Marshal.ThrowExceptionForHR(MFCreateSample(out var sample));
        return sample;
    }

    public static IMFSample CreateTrackedSample()
    {
        Marshal.ThrowExceptionForHR(MFCreateTrackedSample(out var sample));
        return sample;
    }

    public static IMFMediaBuffer CreateMemoryBuffer(uint maxLength)
    {
        Marshal.ThrowExceptionForHR(MFCreateMemoryBuffer(maxLength, out var buffer));
        return buffer;
    }

    public static IMFMediaBuffer CreateDxgiSurfaceBuffer(nint texture)
    {
        Marshal.ThrowExceptionForHR(MFCreateDXGISurfaceBuffer(MediaFoundationGuids.IID_ID3D11Texture2D, texture, 0, 0, out var buffer));
        return buffer;
    }

    public static IMFSinkWriter CreateSinkWriterFromURL(string outputUrl, IMFAttributes attributes)
    {
        Marshal.ThrowExceptionForHR(MFCreateSinkWriterFromURL(outputUrl, 0, attributes, out var writer));
        return writer;
    }

    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
            Marshal.FinalReleaseComObject(comObject);
    }

    public static HardwareDeviceContext CreateHardwareDeviceContextFromDevice(nint externalDevice)
    {
        IMFDXGIDeviceManager? manager = null;
        nint context = 0;
        try
        {
            context = D3D11Unsafe.GetImmediateContext(externalDevice);
            if (Marshal.GetObjectForIUnknown(context) is ID3D10Multithread multithread)
            {
                multithread.SetMultithreadProtected(1);
                Release(multithread);
            }
            Marshal.ThrowExceptionForHR(MFCreateDXGIDeviceManager(out var resetToken, out manager));
            manager.ResetDevice(externalDevice, resetToken);
            Marshal.AddRef(externalDevice);
            return new HardwareDeviceContext(externalDevice, context, manager);
        }
        catch
        {
            Release(manager);
            if (context != 0) Marshal.Release(context);
            throw;
        }
    }

    public static HardwareDeviceContext CreateHardwareDeviceContext()
    {
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        const uint D3D11_CREATE_DEVICE_VIDEO_SUPPORT = 0x800;
        const uint D3D11_SDK_VERSION = 7;

        nint device = 0;
        nint context = 0;
        IMFDXGIDeviceManager? manager = null;
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, D3D_DRIVER_TYPE_HARDWARE, 0,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
                0, 0, D3D11_SDK_VERSION, out device, out _, out context));

            if (Marshal.GetObjectForIUnknown(context) is ID3D10Multithread multithread)
            {
                multithread.SetMultithreadProtected(1);
                Release(multithread);
            }

            Marshal.ThrowExceptionForHR(MFCreateDXGIDeviceManager(out var resetToken, out manager));
            manager.ResetDevice(device, resetToken);
            return new HardwareDeviceContext(device, context, manager);
        }
        catch
        {
            Release(manager);
            if (context != 0) Marshal.Release(context);
            if (device != 0) Marshal.Release(device);
            throw;
        }
    }
}

internal sealed class HardwareDeviceContext(nint device, nint context, IMFDXGIDeviceManager manager) : IDisposable
{
    public IMFDXGIDeviceManager Manager { get; } = manager;
    private nint _device = device;
    private nint _context = context;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        MediaFoundationApi.Release(Manager);
        if (_context != 0) { Marshal.Release(_context); _context = 0; }
        if (_device != 0) { Marshal.Release(_device); _device = 0; }
    }
}

internal static class MediaFoundationGuids
{
    public static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid MF_MT_FRAME_RATE = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    public static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    public static readonly Guid MF_MT_INTERLACE_MODE = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    public static readonly Guid MF_MT_AVG_BITRATE = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    public static readonly Guid MF_MT_MPEG2_PROFILE = new("ad76a80b-2d5c-4e0b-b375-64e520137036");
    public static readonly Guid MF_MT_MPEG2_LEVEL = new("96f66574-11c5-4015-8666-bff516436da7");
    public static readonly Guid MF_MT_DEFAULT_STRIDE = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    public static readonly Guid MF_MT_VIDEO_PRIMARIES = new("dbfbe4d7-0740-4ee0-8192-850ab0e21935");
    public static readonly Guid MF_MT_TRANSFER_FUNCTION = new("5fb0fce9-be5c-4935-a811-ec838f8eed93");
    public static readonly Guid MF_MT_VIDEO_NOMINAL_RANGE = new("c21b8ee5-b956-4071-8daf-325edf5cab11");
    public static readonly Guid MF_MT_YUV_MATRIX = new("3e23d450-2c75-4d25-a00e-b91670d12327");
    public static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    public static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    public static readonly Guid MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION = new("7632f0e6-9538-4d61-acda-ea29c8c14456");

    public static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFMediaType_Audio = new("73647561-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_NV12 = new("3231564e-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFAudioFormat_AAC = new("00001610-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFAudioFormat_PCM = new("00000001-0000-0010-8000-00aa00389b71");

    public static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new("a634a91c-822b-41b9-a494-4de4643612b0");
    public static readonly Guid MF_SINK_WRITER_DISABLE_THROTTLING = new("08b845d8-2b74-4afe-9d53-be16d2d5ae4f");
    public static readonly Guid MF_LOW_LATENCY = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
    public static readonly Guid MF_SINK_WRITER_D3D_MANAGER = new("ec822da2-e1e9-4b29-a0d8-563c719f5269");
    public static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    public static readonly Guid IID_ID3D11VideoDevice = new("10ec4d5b-975a-4689-b9e4-d0aac30fe333");

    public static readonly Guid CODECAPI_AVEncCommonRateControlMode = new("1c0608e9-370c-4710-8a58-cb6181c42423");
    public static readonly Guid CODECAPI_AVEncCommonMeanBitRate = new("f7222374-2144-4815-b550-a37f8e12ee52");
    public static readonly Guid CODECAPI_AVEncCommonQuality = new("fcbf57a3-7ea5-4b0c-9644-69b40c39c391");
    public static readonly Guid CODECAPI_AVEncCommonQualityVsSpeed = new("98332df8-03cd-476b-89fa-3f9e442dec9f");
    public static readonly Guid CODECAPI_AVEncNumWorkerThreads = new("b0c8bf60-16f7-4951-a30b-1db1609293d6");
    public static readonly Guid CODECAPI_AVEncMPVGOPSize = new("95f31b26-95a4-41aa-9303-246a7fc6eef1");
    public static readonly Guid CODECAPI_AVEncMPVDefaultBPictureCount = new("8d390aac-dc5c-4200-b57f-814d04babab2");
    public static readonly Guid CODECAPI_AVEncH264CABACEnable = new("ee6cad62-d305-4248-a50e-e1b255f7caf8");
}

internal static unsafe class D3D11Unsafe
{
    private const int SlotGetDevice = 3;
    private const int SlotCreateTexture2D = 5;
    private const int SlotGetDescTexture2D = 10;
    private const int SlotMap = 14;
    private const int SlotUnmap = 15;
    private const int SlotGetImmediateContext = 40;
    private const int SlotCopyResource = 47;
    private const int SlotFlush = 111;

    [StructLayout(LayoutKind.Sequential)]
    public struct Texture2DDesc
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public uint SampleCount;
        public uint SampleQuality;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    private static void* Slot(nint comObject, int slot) => (*(void***)comObject)[slot];

    public static nint GetDevice(nint deviceChild)
    {
        nint device = 0;
        ((delegate* unmanaged[Stdcall]<nint, nint*, void>)Slot(deviceChild, SlotGetDevice))(deviceChild, &device);
        if (device == 0) throw new InvalidOperationException("ID3D11DeviceChild.GetDevice failed.");
        return device;
    }

    public static nint GetImmediateContext(nint device)
    {
        nint context = 0;
        ((delegate* unmanaged[Stdcall]<nint, nint*, void>)Slot(device, SlotGetImmediateContext))(device, &context);
        if (context == 0) throw new InvalidOperationException("ID3D11Device.GetImmediateContext failed.");
        return context;
    }

    public static Texture2DDesc GetTextureDesc(nint texture)
    {
        Texture2DDesc desc = default;
        ((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void>)Slot(texture, SlotGetDescTexture2D))(texture, &desc);
        return desc;
    }

    public static nint CreateTexture2D(nint device, in Texture2DDesc desc)
    {
        nint texture = 0;
        fixed (Texture2DDesc* p = &desc)
        {
            int hr = ((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, nint, nint*, int>)Slot(device, SlotCreateTexture2D))(device, p, 0, &texture);
            Marshal.ThrowExceptionForHR(hr);
        }
        return texture;
    }

    public static void CopyResource(nint context, nint destination, nint source)
    {
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Slot(context, SlotCopyResource))(context, destination, source);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MappedSubresource
    {
        public nint Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    public static bool TryMapNoWait(nint context, nint resource, out MappedSubresource mapped)
    {
        const uint D3D11_MAP_FLAG_DO_NOT_WAIT = 0x100000;
        const int DXGI_ERROR_WAS_STILL_DRAWING = unchecked((int)0x887A000A);
        MappedSubresource local = default;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedSubresource*, int>)Slot(context, SlotMap))(context, resource, 0, 1, D3D11_MAP_FLAG_DO_NOT_WAIT, &local);
        if (hr == DXGI_ERROR_WAS_STILL_DRAWING)
        {
            mapped = default;
            return false;
        }
        Marshal.ThrowExceptionForHR(hr);
        mapped = local;
        return true;
    }

    public static void Flush(nint context)
    {
        ((delegate* unmanaged[Stdcall]<nint, void>)Slot(context, SlotFlush))(context);
    }

    public static void Unmap(nint context, nint resource)
    {
        ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Slot(context, SlotUnmap))(context, resource, 0);
    }

    public static bool SupportsVideo(nint device)
    {
        Guid iid = MediaFoundationGuids.IID_ID3D11VideoDevice;
        if (Marshal.QueryInterface(device, in iid, out var video) < 0 || video == 0)
            return false;
        Marshal.Release(video);
        return true;
    }

    public static nint QueryInterface(nint unknown, in Guid iid)
    {
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out var result));
        return result;
    }
}
