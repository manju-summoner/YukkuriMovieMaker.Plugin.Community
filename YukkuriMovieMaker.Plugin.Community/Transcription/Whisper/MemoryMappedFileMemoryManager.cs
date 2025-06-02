using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace YukkuriMovieMaker.Plugin.Community.Transcription.Whisper
{
    sealed unsafe class MemoryMappedFileMemoryManager : MemoryManager<byte>
    {
        readonly MemoryMappedFile mmf;
        readonly MemoryMappedViewAccessor accessor;
        readonly SafeMemoryMappedViewHandle viewHandle;
        readonly int length;

        bool disposed;

        public MemoryMappedFileMemoryManager(string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(path), "2GB 以上のファイルは扱えません。");

            length = (int)fileInfo.Length;
            mmf = MemoryMappedFile.CreateFromFile(
                            path,
                            FileMode.Open,
                            mapName: null,
                            capacity: length,
                            MemoryMappedFileAccess.Read);

            accessor = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            viewHandle = accessor.SafeMemoryMappedViewHandle;
        }

        public override Span<byte> GetSpan()
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            // SafeHandle が確保している間は OS 側でマッピングが維持される
            void* ptr = viewHandle.DangerousGetHandle().ToPointer();
            return new Span<byte>(ptr, length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if ((uint)elementIndex > length) 
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            byte* ptr = null;
            viewHandle.AcquirePointer(ref ptr);// 参照カウント +1
            return new MemoryHandle(ptr + elementIndex, pinnable: this);
        }

        public override void Unpin()
        {
            // AcquirePointer で増えた参照カウントを戻す
            if (!disposed)
                viewHandle.ReleasePointer();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                accessor.Dispose();
                mmf.Dispose();
            }

            disposed = true;
        }
    }
}
