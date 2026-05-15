using System.Collections.Concurrent;
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

        private static readonly ConcurrentDictionary<string, NotepadImageReference> References = new();
        private static readonly ConcurrentDictionary<string, BitmapSource> BitmapCache = new();
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

        public static NotepadImageReference RegisterFromBytes(byte[] data, string extension)
        {
            var normalizedExt = NormalizeExtension(extension);
            var id = ComputeHash(data);
            if (References.TryGetValue(id, out var existing))
                return existing;

            var cachePath = Path.Combine(CacheDirectory, $"{id}{normalizedExt}");
            if (!File.Exists(cachePath))
                File.WriteAllBytes(cachePath, data);

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
            if (BitmapCache.TryGetValue(id, out var cached))
                return cached;
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
                BitmapCache[id] = bitmap;
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static NotepadImageReference RegisterExistingCacheFile(string cachePath)
        {
            var id = Path.GetFileNameWithoutExtension(cachePath).ToLowerInvariant();
            var extension = NormalizeExtension(Path.GetExtension(cachePath));
            if (References.TryGetValue(id, out var existing))
                return existing;
            var (w, h) = ReadDimensions(cachePath);
            var reference = new NotepadImageReference(id, cachePath, w, h, extension);
            References[id] = reference;
            return reference;
        }

        public static string BuildPlaceholder(string id) => $"{PlaceholderPrefix}{id}{PlaceholderSuffix}";

        private static NotepadImageReference? TryRehydrateFromDisk(string id)
        {
            try
            {
                var dir = new DirectoryInfo(CacheDirectory);
                if (!dir.Exists)
                    return null;
                foreach (var file in dir.EnumerateFiles($"{id}.*"))
                {
                    var (w, h) = ReadDimensions(file.FullName);
                    var reference = new NotepadImageReference(id, file.FullName, w, h, file.Extension);
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

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return ".png";
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith('.'))
                ext = "." + ext;
            return ext switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".tif" => ext,
                _ => ".png"
            };
        }
    }
}
