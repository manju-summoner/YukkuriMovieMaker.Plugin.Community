using System.IO;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3BStream : Vst3HostObject
    {
        readonly MemoryStream stream;

        public Vst3BStream(byte[]? data = null) : base(Vst3Native.IBStreamUid)
        {
            stream = data is null ? new MemoryStream() : new MemoryStream(data.ToArray());
            BuildVtable(
                new ReadDelegate(Read),
                new WriteDelegate(Write),
                new SeekDelegate(Seek),
                new TellDelegate(Tell));
        }

        public byte[] ToArray() => stream.ToArray();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int ReadDelegate(IntPtr self, IntPtr buffer, int numBytes, IntPtr numBytesRead);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int WriteDelegate(IntPtr self, IntPtr buffer, int numBytes, IntPtr numBytesWritten);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int SeekDelegate(IntPtr self, long position, int mode, IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate int TellDelegate(IntPtr self, IntPtr position);

        int Read(IntPtr self, IntPtr buffer, int numBytes, IntPtr numBytesRead)
        {
            var count = 0;
            if (numBytes > 0 && buffer != IntPtr.Zero)
            {
                var data = new byte[numBytes];
                count = stream.Read(data, 0, numBytes);
                Marshal.Copy(data, 0, buffer, count);
            }
            if (numBytesRead != IntPtr.Zero)
                Marshal.WriteInt32(numBytesRead, count);
            return Vst3Native.ResultOk;
        }

        int Write(IntPtr self, IntPtr buffer, int numBytes, IntPtr numBytesWritten)
        {
            var count = 0;
            if (numBytes > 0 && buffer != IntPtr.Zero)
            {
                var data = new byte[numBytes];
                Marshal.Copy(buffer, data, 0, numBytes);
                stream.Write(data, 0, numBytes);
                count = numBytes;
            }
            if (numBytesWritten != IntPtr.Zero)
                Marshal.WriteInt32(numBytesWritten, count);
            return Vst3Native.ResultOk;
        }

        int Seek(IntPtr self, long position, int mode, IntPtr result)
        {
            var origin = mode switch
            {
                1 => SeekOrigin.Current,
                2 => SeekOrigin.End,
                _ => SeekOrigin.Begin,
            };
            var newPosition = stream.Seek(position, origin);
            if (result != IntPtr.Zero)
                Marshal.WriteInt64(result, newPosition);
            return Vst3Native.ResultOk;
        }

        int Tell(IntPtr self, IntPtr position)
        {
            if (position != IntPtr.Zero)
                Marshal.WriteInt64(position, stream.Position);
            return Vst3Native.ResultOk;
        }
    }
}
