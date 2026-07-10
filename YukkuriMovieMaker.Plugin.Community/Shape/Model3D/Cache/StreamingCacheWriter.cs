using System.IO;
using System.Numerics;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class StreamingCacheWriter : IStreamingCacheWriter
{
    private readonly string _cacheDir;
    private readonly string _tempDir;
    private readonly bool _isSplit;
    private Stream? _stream;
    private BinaryWriter? _writer;
    private bool _committed;
    private bool _disposed;

    private const string TempDirPrefix = ".tmp";
    private static readonly TimeSpan StaleTempDirAge = TimeSpan.FromHours(1);

    public StreamingCacheWriter(string cacheDir, bool isSplit)
    {
        _cacheDir = cacheDir;
        _isSplit = isSplit;
        _tempDir = Path.Combine(cacheDir, TempDirPrefix + "-" + Guid.NewGuid().ToString("N"));
        _committed = false;

        CleanStaleTempDirs(cacheDir);
        Directory.CreateDirectory(_tempDir);

        if (_isSplit)
        {
            _stream = new SplitWriteStream(_tempDir, ModelCacheFormat.SplitChunkSize);
        }
        else
        {
            string tempFile = Path.Combine(_tempDir, ModelCacheFormat.SingleFileName);
            _stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        }
        _writer = new BinaryWriter(_stream);
    }

    public void WriteHeader(CacheHeader header)
    {
        EnsureNotDisposed();
        ModelCacheFormat.WriteHeader(_writer!, header);
    }

    public void WriteMetadata(int vertexCount, int indexCount, List<Model3DPart> parts, Vector3 center, float scale, IReadOnlyList<string> dependencies)
    {
        EnsureNotDisposed();
        ModelCacheFormat.WriteCounts(_writer!, vertexCount, indexCount, parts.Count);

        foreach (var part in parts)
            ModelCacheFormat.WritePart(_writer!, part);

        ModelCacheFormat.WriteTransform(_writer!, center, scale);
        ModelCacheFormat.WriteDependencies(_writer!, dependencies);
    }

    public void WriteVertexChunk(ReadOnlySpan<byte> vertexData)
    {
        EnsureNotDisposed();
        _writer!.Flush();
        _stream!.Write(vertexData);
    }

    public void WriteIndexChunk(ReadOnlySpan<byte> indexData)
    {
        EnsureNotDisposed();
        _writer!.Flush();
        _stream!.Write(indexData);
    }

    public void Commit()
    {
        EnsureNotDisposed();
        if (_committed) return;

        _writer?.Flush();
        _stream?.Flush();
        _writer?.Dispose();
        _stream?.Dispose();
        _writer = null;
        _stream = null;

        try
        {
            if (_isSplit)
            {
                CleanExistingSplitFiles(_cacheDir);
                CleanExistingSingleFile(_cacheDir);

                foreach (var tmpFile in Directory.GetFiles(_tempDir, ModelCacheFormat.SplitFilePattern))
                {
                    string fileName = Path.GetFileName(tmpFile);
                    string dest = Path.Combine(_cacheDir, fileName);
                    File.Move(tmpFile, dest, true);
                }
            }
            else
            {
                string tmpFile = Path.Combine(_tempDir, ModelCacheFormat.SingleFileName);
                string destFile = Path.Combine(_cacheDir, ModelCacheFormat.SingleFileName);
                File.Move(tmpFile, destFile, true);

                CleanExistingSplitFiles(_cacheDir);
            }

            _committed = true;
        }
        finally
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }
    }

    public void Rollback()
    {
        try
        {
            _writer?.Dispose();
            _stream?.Dispose();
        }
        catch { }
        _writer = null;
        _stream = null;

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private static void CleanStaleTempDirs(string cacheDir)
    {
        try
        {
            var threshold = DateTime.UtcNow - StaleTempDirAge;
            foreach (var dir in Directory.GetDirectories(cacheDir, TempDirPrefix + "*"))
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(dir) < threshold)
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void CleanExistingSplitFiles(string dir)
    {
        try
        {
            foreach (var f in Directory.GetFiles(dir, ModelCacheFormat.SplitFilePattern))
            {
                File.Delete(f);
            }
        }
        catch { }
    }

    private static void CleanExistingSingleFile(string dir)
    {
        try
        {
            string singleFile = Path.Combine(dir, ModelCacheFormat.SingleFileName);
            if (File.Exists(singleFile))
            {
                File.Delete(singleFile);
            }
        }
        catch { }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamingCacheWriter));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_committed)
        {
            Rollback();
        }
        else
        {
            try
            {
                _writer?.Dispose();
                _stream?.Dispose();
            }
            catch { }
        }
    }

    private sealed class SplitWriteStream : Stream
    {
        private readonly string _dir;
        private readonly int _chunkSize;
        private int _partIndex;
        private FileStream? _currentStream;
        private long _totalLength;

        public SplitWriteStream(string dir, int chunkSize)
        {
            _dir = dir;
            _chunkSize = chunkSize;
            NextPart();
        }

        private void NextPart()
        {
            _currentStream?.Dispose();
            string path = Path.Combine(_dir, ModelCacheFormat.GetSplitFileName(_partIndex));
            _currentStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            _partIndex++;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _totalLength;
        public override long Position { get => _totalLength; set => throw new NotSupportedException(); }

        public override void Flush() => _currentStream?.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            int written = 0;
            while (written < buffer.Length)
            {
                if (_currentStream!.Length >= _chunkSize)
                {
                    NextPart();
                }

                int remainingInChunk = (int)(_chunkSize - _currentStream!.Length);
                int toWrite = Math.Min(remainingInChunk, buffer.Length - written);
                _currentStream!.Write(buffer.Slice(written, toWrite));
                written += toWrite;
                _totalLength += toWrite;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _currentStream?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
