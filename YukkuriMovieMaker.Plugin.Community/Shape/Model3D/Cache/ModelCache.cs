using System.IO;
using System.Security.Cryptography;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;

internal sealed class ModelCache
{
    private const string CacheDirName = ".cache";
    private const int MaxIoChunkBytes = 1 << 30;

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
            if (File.Exists(Path.Combine(cacheDir, ModelCacheFormat.SingleFileName))) isSplit = false;
            else if (File.Exists(Path.Combine(cacheDir, ModelCacheFormat.GetSplitFileName(0)))) isSplit = true;
            else return false;

            Stream stream = isSplit
                ? new MultiFileStream(cacheDir)
                : new FileStream(Path.Combine(cacheDir, ModelCacheFormat.SingleFileName), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

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
            string fileHash = ComputeFileHash(path);
            var header = new CacheHeader(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash);

            using var writer = CreateStreamingWriter(path, header);
            writer.WriteMetadata(model.Vertices.Length, model.Indices.Length, model.Parts, model.ModelCenter, model.ModelScale);
            WriteBody(writer, model);
            writer.Commit();
        }
        catch
        {
        }
    }

    private static unsafe void WriteBody(IStreamingCacheWriter writer, Model3DData model)
    {
        fixed (Model3DVertex* pV = model.Vertices)
        {
            WriteChunked((byte*)pV, (long)model.Vertices.Length * sizeof(Model3DVertex), writer.WriteVertexChunk);
        }

        fixed (int* pI = model.Indices)
        {
            WriteChunked((byte*)pI, (long)model.Indices.Length * sizeof(int), writer.WriteIndexChunk);
        }
    }

    private static unsafe void WriteChunked(byte* pointer, long length, CacheChunkWriter write)
    {
        long written = 0;
        while (written < length)
        {
            int chunk = (int)Math.Min(length - written, MaxIoChunkBytes);
            write(new ReadOnlySpan<byte>(pointer + written, chunk));
            written += chunk;
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
            ReadExact(stream, (byte*)pV, (long)vCount * sizeof(Model3DVertex));
        }

        fixed (int* pI = indices)
        {
            ReadExact(stream, (byte*)pI, (long)iCount * sizeof(int));
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

    private static unsafe void ReadExact(Stream stream, byte* pointer, long length)
    {
        long totalRead = 0;
        while (totalRead < length)
        {
            int chunk = (int)Math.Min(length - totalRead, MaxIoChunkBytes);
            int read = stream.Read(new Span<byte>(pointer + totalRead, chunk));
            if (read == 0) break;
            totalRead += read;
        }
        if (totalRead != length)
            throw new InvalidDataException($"Expected {length} bytes, read {totalRead}");
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
            string path = Path.Combine(_baseDir, ModelCacheFormat.GetSplitFileName(_currentIndex));
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
}
