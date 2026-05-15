using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal static class NotepadImageCache
    {
        public const string PlaceholderPrefix = "\ufffc[image:";
        public const string PlaceholderSuffix = "]";

        private const int MaxBitmapCacheEntries = 256;
        private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif"];

        private static readonly ConcurrentDictionary<string, NotepadImageReference> References = new();
        private static readonly LinkedList<string> BitmapAccessOrder = new();
        private static readonly Dictionary<string, LinkedListNode<string>> BitmapAccessNodes = new();
        private static readonly Dictionary<string, BitmapSource> BitmapCache = new();
        private static readonly object BitmapCacheLock = new();
        private static readonly object InitLock = new();
        private static string? cacheDirectory;

        public static string CacheDirectory
        {
            get
            {
                if (cacheDirectory is not null)
                    return cacheDirectory;
                lock (InitLock)
                {
                    if (cacheDirectory is not null)
                        return cacheDirectory;
                    var dir = Path.Combine(AppDirectories.UserResourceDirectory, "cache", "NotepadImages");
                    Directory.CreateDirectory(dir);
                    cacheDirectory = dir;
                }
                return cacheDirectory;
            }
        }

        public static bool IsValidImageId(string id) =>
            !string.IsNullOrEmpty(id) &&
            id.Length <= 128 &&
            id.All(static c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        public static string GetSupportedImageFileFilter() =>
            string.Join(';', AllowedExtensions.Select(static ext => $"*{ext}"));

        public static bool TryNormalizeExtension(string extension, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(extension))
                return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith('.'))
                ext = "." + ext;
            if (!AllowedExtensions.Contains(ext))
                return false;
            normalized = ext;
            return true;
        }

        public static NotepadImageReference RegisterFromBytes(byte[] data, string extension)
        {
            var normalizedExt = TryNormalizeExtension(extension, out var ext) ? ext : ".png";
            var id = ComputeHash(data);
            if (References.TryGetValue(id, out var existing))
                return existing;

            var cachePath = Path.Combine(CacheDirectory, $"{id}{normalizedExt}");
            WriteCacheFileAtomically(cachePath, data);

            var (w, h) = ReadDimensions(cachePath);
            var reference = new NotepadImageReference(id, cachePath, w, h, normalizedExt);
            References[id] = reference;
            return reference;
        }

        public static NotepadImageReference RegisterFromFile(string sourcePath)
        {
            var bytes = File.ReadAllBytes(sourcePath);
            var ext = Path.GetExtension(sourcePath);
            return RegisterFromBytes(bytes, ext);
        }

        public static NotepadImageReference RegisterFromBitmap(BitmapSource bitmap)
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return RegisterFromBytes(ms.ToArray(), ".png");
        }

        public static bool TryGet(string id, out NotepadImageReference reference)
        {
            if (References.TryGetValue(id, out var found))
            {
                reference = found;
                return true;
            }
            var existing = TryRehydrateFromDisk(id);
            if (existing is not null)
            {
                reference = existing;
                return true;
            }
            reference = null!;
            return false;
        }

        public static BitmapSource? GetBitmap(string id)
        {
            lock (BitmapCacheLock)
            {
                if (BitmapCache.TryGetValue(id, out var cached))
                {
                    TouchBitmapAccessOrder(id);
                    return cached;
                }
            }
            if (!TryGet(id, out var reference))
                return null;
            try
            {
                BitmapImage bitmap;
                using (var stream = new FileStream(reference.CachePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                StoreBitmap(id, bitmap);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static NotepadImageReference? RegisterExistingCacheFile(string cachePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(cachePath);
            var id = fileName.ToLowerInvariant();
            if (!IsValidImageId(id))
                return null;
            if (!TryNormalizeExtension(Path.GetExtension(cachePath), out var extension))
                return null;
            if (References.TryGetValue(id, out var existing))
                return existing;
            var (w, h) = ReadDimensions(cachePath);
            var reference = new NotepadImageReference(id, cachePath, w, h, extension);
            References[id] = reference;
            return reference;
        }

        public static string BuildPlaceholder(string id) => $"{PlaceholderPrefix}{id}{PlaceholderSuffix}";

        private static void WriteCacheFileAtomically(string cachePath, byte[] data)
        {
            if (File.Exists(cachePath))
                return;
            var tempPath = Path.Combine(CacheDirectory, $"{Path.GetFileName(cachePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(tempPath, data);
                File.Move(tempPath, cachePath, overwrite: true);
            }
            catch (IOException) when (File.Exists(cachePath))
            {
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static void StoreBitmap(string id, BitmapSource bitmap)
        {
            lock (BitmapCacheLock)
            {
                if (BitmapCache.ContainsKey(id))
                {
                    BitmapCache[id] = bitmap;
                    TouchBitmapAccessOrder(id);
                    return;
                }
                while (BitmapCache.Count >= MaxBitmapCacheEntries && BitmapAccessOrder.First is { } oldest)
                {
                    BitmapAccessOrder.RemoveFirst();
                    BitmapAccessNodes.Remove(oldest.Value);
                    BitmapCache.Remove(oldest.Value);
                }
                BitmapCache[id] = bitmap;
                BitmapAccessNodes[id] = BitmapAccessOrder.AddLast(id);
            }
        }

        private static void TouchBitmapAccessOrder(string id)
        {
            if (!BitmapAccessNodes.TryGetValue(id, out var node))
                return;
            BitmapAccessOrder.Remove(node);
            BitmapAccessOrder.AddLast(node);
        }

        private static NotepadImageReference? TryRehydrateFromDisk(string id)
        {
            if (!IsValidImageId(id))
                return null;
            try
            {
                var dir = new DirectoryInfo(CacheDirectory);
                if (!dir.Exists)
                    return null;
                foreach (var file in dir.EnumerateFiles($"{id}.*"))
                {
                    if (!TryNormalizeExtension(file.Extension, out var extension))
                        continue;
                    var (w, h) = ReadDimensions(file.FullName);
                    var reference = new NotepadImageReference(id, file.FullName, w, h, extension);
                    References[id] = reference;
                    return reference;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ComputeHash(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static (int Width, int Height) ReadDimensions(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
