using System.IO;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal static class DiskTypeDetector
{
    private static readonly Dictionary<string, DiskType> DriveTypeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock CacheLock = new();

    public static DiskType GetDiskType(string path)
    {
        if (string.IsNullOrEmpty(path)) return DiskType.Unknown;

        try
        {
            string? root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return DiskType.Unknown;

            string driveLetter = root.TrimEnd('\\');

            lock (CacheLock)
            {
                if (DriveTypeCache.TryGetValue(driveLetter, out var cachedType))
                {
                    return cachedType;
                }
            }

            var result = DetectDiskTypeViaIoctl(driveLetter);

            lock (CacheLock)
            {
                DriveTypeCache[driveLetter] = result;
            }
            return result;
        }
        catch
        {
        }

        return DiskType.Unknown;
    }

    private static DiskType DetectDiskTypeViaIoctl(string driveLetter)
    {
        string volumePath = string.Concat(@"\\.\", driveLetter);

        IntPtr hDevice = CreateFile(
            volumePath,
            0,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hDevice == INVALID_HANDLE_VALUE)
        {
            return DiskType.Unknown;
        }

        try
        {
            int querySize = Marshal.SizeOf(typeof(STORAGE_PROPERTY_QUERY));
            IntPtr queryPtr = Marshal.AllocHGlobal(querySize);
            try
            {
                unsafe
                {
                    byte* pQuery = (byte*)queryPtr.ToPointer();
                    for (int i = 0; i < querySize; i++) pQuery[i] = 0;
                }

                Marshal.WriteInt32(queryPtr, 0, StorageDeviceSeekPenaltyProperty);
                Marshal.WriteInt32(queryPtr, 4, PropertyStandardQuery);

                int outSize = 256;
                IntPtr outPtr = Marshal.AllocHGlobal(outSize);
                try
                {
                    bool success = DeviceIoControl(
                        hDevice,
                        IOCTL_STORAGE_QUERY_PROPERTY,
                        queryPtr,
                        querySize,
                        outPtr,
                        outSize,
                        out int bytesReturned,
                        IntPtr.Zero);

                    if (!success)
                    {
                        return DiskType.Unknown;
                    }

                    if (bytesReturned < Marshal.SizeOf(typeof(DEVICE_SEEK_PENALTY_DESCRIPTOR)))
                    {
                        return DiskType.Unknown;
                    }

                    var descriptor = Marshal.PtrToStructure<DEVICE_SEEK_PENALTY_DESCRIPTOR>(outPtr);
                    return descriptor.IncursSeekPenalty ? DiskType.Hdd : DiskType.Ssd;
                }
                finally
                {
                    Marshal.FreeHGlobal(outPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(queryPtr);
            }
        }
        finally
        {
            CloseHandle(hDevice);
        }
    }

    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    private const int PropertyStandardQuery = 0;
    private const int StorageDeviceSeekPenaltyProperty = 7;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)]
        public bool IncursSeekPenalty;
    }
}
