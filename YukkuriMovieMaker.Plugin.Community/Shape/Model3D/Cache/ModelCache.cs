using System.IO;
using System.Security.Cryptography;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class ModelCache
{
    private const string CacheDirName = ".cache";

    public IStreamingCacheWriter CreateStreamingWriter(string path, CacheHeader header)
    {
        string root = Path.GetDirectoryName(path) ?? string.Empty;
        string hash = ComputePathHash(path);
        string cacheDir = Path.Combine(root, CacheDirName);
        string modelCacheDir = Path.Combine(cacheDir, hash);

        EnsureCacheDirectories(cacheDir, modelCacheDir);

        bool isSplit = DiskTypeDetector.GetDiskType(root) == DiskType.Hdd;
        var writer = new StreamingCacheWriter(modelCacheDir, isSplit);
        writer.WriteHeader(header);
        return writer;
    }

    public bool TryLoad(string path, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion, out Model3DData model)
    {
        model = new Model3DData();

        try
        {
            string root = Path.GetDirectoryName(path) ?? string.Empty;
            string hash = ComputePathHash(path);
            string cacheDir = Path.Combine(root, CacheDirName, hash);
            if (!Directory.Exists(cacheDir)) return false;

            bool isSplit;
            if (File.Exists(Path.Combine(cacheDir, "model.bin"))) isSplit = false;
            else if (File.Exists(Path.Combine(cacheDir, "part.0.bin"))) isSplit = true;
            else return false;

            Stream stream = isSplit
                ? new MultiFileStream(cacheDir)
                : new FileStream(Path.Combine(cacheDir, "model.bin"), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

            string fileHash = ComputeFileHash(path);

            using (stream)
            using (var br = new BinaryReader(stream))
            {
                var header = ModelCacheFormat.ReadHeader(br);
                if (!header.IsValid(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash)) return false;

                model = ReadBody(br, stream);
            }

            return model.Vertices.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public void Save(string path, Model3DData model, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion)
    {
        try
        {
            string root = Path.GetDirectoryName(path) ?? string.Empty;
            string hash = ComputePathHash(path);
            string cacheDir = Path.Combine(root, CacheDirName);
            string modelCacheDir = Path.Combine(cacheDir, hash);

            EnsureCacheDirectories(cacheDir, modelCacheDir);

            bool isSplit = DiskTypeDetector.GetDiskType(root) == DiskType.Hdd;

            string fileHash = ComputeFileHash(path);
            var header = new CacheHeader(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash);

            if (!isSplit)
            {
                string tempPath = Path.Combine(modelCacheDir, "model.bin.tmp");
                string finalPath = Path.Combine(modelCacheDir, "model.bin");

                WriteCacheFileSingle(tempPath, header, model);
                File.Move(tempPath, finalPath, true);

                CleanUpSplitFiles(modelCacheDir);
            }
            else
            {
                CleanUpSplitFiles(modelCacheDir);
                WriteCacheFileSplit(modelCacheDir, header, model);
                string singleFile = Path.Combine(modelCacheDir, "model.bin");
                if (File.Exists(singleFile)) File.Delete(singleFile);
            }
        }
        catch
        {
        }
    }

    private static void EnsureCacheDirectories(string cacheDir, string modelCacheDir)
    {
        if (!Directory.Exists(cacheDir))
        {
            var di = Directory.CreateDirectory(cacheDir);
            di.Attributes |= FileAttributes.Hidden;
        }
        if (!Directory.Exists(modelCacheDir))
        {
            Directory.CreateDirectory(modelCacheDir);
        }
    }

    private static void CleanUpSplitFiles(string dir)
    {
        foreach (var f in Directory.GetFiles(dir, "part.*.bin")) File.Delete(f);
    }

    public static string ComputePathHash(string path)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
        byte[] hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    public static string ComputeFileHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static unsafe Model3DData ReadBody(BinaryReader br, Stream stream)
    {
        var limits = Model3DSettings.Default;

        int vCount = br.ReadInt32();
        int iCount = br.ReadInt32();
        int pCount = br.ReadInt32();

        if (vCount < 0 || vCount > limits.MaxVertices)
            throw new InvalidDataException($"Invalid vertex count: {vCount}");
        if (iCount < 0 || iCount > limits.MaxIndices)
            throw new InvalidDataException($"Invalid index count: {iCount}");
        if (pCount < 0 || pCount > limits.MaxParts)
            throw new InvalidDataException($"Invalid part count: {pCount}");

        var parts = new List<Model3DPart>(pCount);
        for (int i = 0; i < pCount; i++)
            parts.Add(ModelCacheFormat.ReadPart(br));

        var (center, scale) = ModelCacheFormat.ReadTransform(br);

        var vertices = GC.AllocateUninitializedArray<Model3DVertex>(vCount, true);
        var indices = GC.AllocateUninitializedArray<int>(iCount, true);

        fixed (Model3DVertex* pV = vertices)
        {
            ReadExact(stream, new Span<byte>(pV, vCount * sizeof(Model3DVertex)));
        }

        fixed (int* pI = indices)
        {
            ReadExact(stream, new Span<byte>(pI, iCount * sizeof(int)));
        }

        return new Model3DData
        {
            Vertices = vertices,
            Indices = indices,
            Parts = parts,
            ModelCenter = center,
            ModelScale = scale
        };
    }

    private static void ReadExact(Stream stream, Span<byte> span)
    {
        int totalRead = 0;
        while (totalRead < span.Length)
        {
            int read = stream.Read(span.Slice(totalRead));
            if (read == 0) break;
            totalRead += read;
        }
        if (totalRead != span.Length)
            throw new InvalidDataException($"Expected {span.Length} bytes, read {totalRead}");
    }

    private static void WriteCacheFileSingle(string path, CacheHeader header, Model3DData model)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs);
        WriteData(bw, header, model);
    }

    private static void WriteCacheFileSplit(string dir, CacheHeader header, Model3DData model)
    {
        const int ChunkSize = 256 * 1024;
        using var splitter = new SplitStream(dir, ChunkSize);
        using var bw = new BinaryWriter(splitter);
        WriteData(bw, header, model);
    }

    private static unsafe void WriteData(BinaryWriter bw, CacheHeader header, Model3DData model)
    {
        ModelCacheFormat.WriteHeader(bw, header);
        ModelCacheFormat.WriteCounts(bw, model.Vertices.Length, model.Indices.Length, model.Parts.Count);

        foreach (var part in model.Parts)
            ModelCacheFormat.WritePart(bw, part);

        ModelCacheFormat.WriteTransform(bw, model.ModelCenter, model.ModelScale);

        fixed (Model3DVertex* pV = model.Vertices)
        {
            bw.Write(new ReadOnlySpan<byte>(pV, model.Vertices.Length * sizeof(Model3DVertex)));
        }

        fixed (int* pI = model.Indices)
        {
            bw.Write(new ReadOnlySpan<byte>(pI, model.Indices.Length * sizeof(int)));
        }
    }

    private sealed class MultiFileStream : Stream
    {
        private readonly string _baseDir;
        private int _currentIndex;
        private FileStream? _currentStream;
        private long _position;

        public MultiFileStream(string baseDir)
        {
            _baseDir = baseDir;
            OpenNextStream();
        }

        private void OpenNextStream()
        {
            _currentStream?.Dispose();
            string path = Path.Combine(_baseDir, $"part.{_currentIndex}.bin");
            _currentStream = File.Exists(path)
                ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan)
                : null;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_currentStream == null) return 0;

            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = _currentStream.Read(buffer.Slice(totalRead));
                if (read == 0)
                {
                    _currentIndex++;
                    OpenNextStream();
                    if (_currentStream == null) break;
                }
                else
                {
                    totalRead += read;
                    _position += read;
                }
            }
            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _currentStream?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    private sealed class SplitStream : Stream
    {
        private readonly string _dir;
        private readonly int _chunkSize;
        private int _partIndex;
        private FileStream? _currentStream;
        private long _totalLength;

        public SplitStream(string dir, int chunkSize)
        {
            _dir = dir;
            _chunkSize = chunkSize;
            NextPart();
        }

        private void NextPart()
        {
            _currentStream?.Dispose();
            string path = Path.Combine(_dir, $"part.{_partIndex}.bin");
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

        public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

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
